using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/* ---------- Jednokratno geokodiranje narudžbi bez koordinata ---------- */

public record GeocodeMissingCommand : IRequest<GeocodeMissingResult>;

/// <summary>
/// <paramref name="Obradjeno"/> je broj narudžbi koje je ovaj prolaz pokušao geokodirati,
/// <paramref name="Uspjesno"/> koliko ih je stvarno dobilo koordinate. Razlika su adrese
/// koje Nominatim ne prepoznaje — njih treba postaviti ručno.
/// </summary>
public record GeocodeMissingResult(int Obradjeno, int Uspjesno);

public class GeocodeMissingHandler(IAppDbContext db, IGeocodingService geocoder)
    : IRequestHandler<GeocodeMissingCommand, GeocodeMissingResult>
{
    /// <summary>
    /// Jedan pritisak = jedna ograničena serija. Geokodiranje ide ~1 sekundu po adresi uz
    /// otvorenu HTTP vezu (reverse proxy puca na 60 s), a Nominatimova pravila korištenja
    /// zabranjuju masovno geokodiranje spiskova adresa. Admin ponavlja dok ima šta ostalo.
    /// </summary>
    private const int BatchSize = 25;

    public async Task<GeocodeMissingResult> Handle(GeocodeMissingCommand cmd, CancellationToken ct)
    {
        var narudzbe = await db.Orders
            .Where(o => o.Latitude == null && o.DeliveryAddress != "")
            .OrderByDescending(o => o.CreatedAt)
            .Take(BatchSize)
            .Select(o => new { o.Id, o.DeliveryAddress })
            .ToListAsync(ct);

        var uspjesno = 0;
        foreach (var n in narudzbe)
        {
            var tacka = await geocoder.GeocodeAsync($"{n.DeliveryAddress}, Banja Luka", ct);
            if (tacka is null) continue;
            if (!GeoBounds.Contains(tacka.Value.Lat, tacka.Value.Lon)) continue;

            var order = await db.Orders.FirstAsync(o => o.Id == n.Id, ct);
            order.Latitude = tacka.Value.Lat;
            order.Longitude = tacka.Value.Lon;
            await db.SaveChangesAsync(ct);
            uspjesno++;
        }

        return new GeocodeMissingResult(narudzbe.Count, uspjesno);
    }
}

/* ---------- Ručna ispravka koordinata na jednoj narudžbi ---------- */

public record SetOrderCoordinatesCommand(Guid OrderId, double? Latitude, double? Longitude)
    : IRequest<Unit>;

public class SetOrderCoordinatesHandler(IAppDbContext db)
    : IRequestHandler<SetOrderCoordinatesCommand, Unit>
{
    public async Task<Unit> Handle(SetOrderCoordinatesCommand cmd, CancellationToken ct)
    {
        // Tijelo zahtjeva može stići nepotpuno ({}), a nula je stvarna tačka u Atlantiku —
        // nikad zamjena za "nepoznato".
        if (cmd.Latitude is not { } lat || cmd.Longitude is not { } lon)
            throw new DomainException(
                "Potrebne su i geografska širina i dužina.", "invalid_coordinates");

        if (lat is < -90 or > 90)
            throw new DomainException("Neispravna geografska širina.", "invalid_coordinates");
        if (lon is < -180 or > 180)
            throw new DomainException("Neispravna geografska dužina.", "invalid_coordinates");
        if (!GeoBounds.Contains(lat, lon))
            throw new DomainException(GeoBounds.OutsideMessage, "invalid_coordinates");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        order.Latitude = lat;
        order.Longitude = lon;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
