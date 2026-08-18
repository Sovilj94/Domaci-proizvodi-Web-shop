using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/* ---------- Jednokratno geokodiranje narudžbi bez koordinata ---------- */

public record GeocodeMissingCommand : IRequest<GeocodeMissingResult>;

public record GeocodeMissingResult(bool Pokrenuto, int BrojNarudzbi);

public class GeocodeMissingHandler(IAppDbContext db, IGeocodingService geocoder)
    : IRequestHandler<GeocodeMissingCommand, GeocodeMissingResult>
{
    public async Task<GeocodeMissingResult> Handle(GeocodeMissingCommand cmd, CancellationToken ct)
    {
        var narudzbe = await db.Orders
            .Where(o => o.Latitude == null && o.DeliveryAddress != "")
            .Select(o => new { o.Id, o.DeliveryAddress })
            .ToListAsync(ct);

        foreach (var n in narudzbe)
        {
            var tacka = await geocoder.GeocodeAsync($"{n.DeliveryAddress}, Banja Luka", ct);
            if (tacka is null) continue;

            var order = await db.Orders.FirstAsync(o => o.Id == n.Id, ct);
            order.Latitude = tacka.Value.Lat;
            order.Longitude = tacka.Value.Lon;
            await db.SaveChangesAsync(ct);
        }

        return new GeocodeMissingResult(true, narudzbe.Count);
    }
}

/* ---------- Ručna ispravka koordinata na jednoj narudžbi ---------- */

public record SetOrderCoordinatesCommand(Guid OrderId, double Latitude, double Longitude)
    : IRequest<Unit>;

public class SetOrderCoordinatesHandler(IAppDbContext db)
    : IRequestHandler<SetOrderCoordinatesCommand, Unit>
{
    public async Task<Unit> Handle(SetOrderCoordinatesCommand cmd, CancellationToken ct)
    {
        if (cmd.Latitude is < -90 or > 90)
            throw new DomainException("Neispravna geografska širina.", "invalid_coordinates");
        if (cmd.Longitude is < -180 or > 180)
            throw new DomainException("Neispravna geografska dužina.", "invalid_coordinates");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        order.Latitude = cmd.Latitude;
        order.Longitude = cmd.Longitude;
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
