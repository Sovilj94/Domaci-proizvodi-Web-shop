using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Naselja;

/// <summary>
/// Naselje za tačku adrese: aktivno naselje čija granica sadrži tačku. Kupac ga ne bira —
/// server ga određuje pri svakoj narudžbi, adresi i pretplati, pa provjera u pregledaču nije
/// jedina brana.
/// </summary>
public static class OdredjivanjeNaselja
{
    public const string KodVan = "van_podrucja_dostave";

    /// <summary>Aktivno naselje koje sadrži tačku, ili null. Na zajedničkoj ivici dva naselja
    /// pobjeđuje prvo po imenu — deterministički, nikad greška.</summary>
    public static async Task<DeliveryZone?> NadjiAsync(IAppDbContext db, double lat, double lon, CancellationToken ct)
    {
        var naselja = await db.DeliveryZones
            .Where(z => z.IsActive && z.Granica != null)
            .ToListAsync(ct);

        return naselja
            .OrderBy(z => z.Name, StringComparer.Ordinal)
            .FirstOrDefault(z => Sadrzi(z, lat, lon));
    }

    /// <summary>Kao <see cref="NadjiAsync"/>, ali tačka van naselja (ili bez tačke) baca
    /// <c>van_podrucja_dostave</c> sa kontaktom administratora iz baze.</summary>
    public static async Task<DeliveryZone> OdrediAsync(IAppDbContext db, double? lat, double? lon, CancellationToken ct)
    {
        if (lat is null || lon is null)
            throw new DomainException("Označi adresu na mapi.", KodVan);

        return await NadjiAsync(db, lat.Value, lon.Value, ct)
            ?? throw new DomainException(await PorukaVanAsync(db, ct), KodVan);
    }

    public static async Task<string> PorukaVanAsync(IAppDbContext db, CancellationToken ct)
    {
        var s = await db.SadrzajSajta.AsNoTracking().FirstOrDefaultAsync(ct);
        var kontakt = new[] { s?.Telefon, s?.Email }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        return "Na ovu adresu trenutno ne vršimo dostavu. Ako misliš da je greška, javi se administratoru"
               + (kontakt.Count > 0 ? ": " + string.Join(", ", kontakt) : "") + ".";
    }

    /// <summary>Neispravna granica u bazi (ručno upisana mimo admina) ne smije oboriti
    /// naručivanje u ostalim naseljima — takvo naselje se samo ne pogađa.</summary>
    internal static bool Sadrzi(DeliveryZone z, double lat, double lon)
    {
        try { return Granica.Sadrzi(Granica.Procitaj(z.Granica!), lat, lon); }
        catch (DomainException) { return false; }
    }
}

/* ---------- javni upit: „u kojem sam naselju?" (checkout, adresar, pretplata) ---------- */

public record NaseljeZaTackuDto(Guid Id, string Name);

public record NaseljeZaTackuQuery(double Lat, double Lon) : IRequest<NaseljeZaTackuDto>;

public class NaseljeZaTackuHandler(IAppDbContext db) : IRequestHandler<NaseljeZaTackuQuery, NaseljeZaTackuDto>
{
    public async Task<NaseljeZaTackuDto> Handle(NaseljeZaTackuQuery q, CancellationToken ct)
    {
        var z = await OdredjivanjeNaselja.OdrediAsync(db, q.Lat, q.Lon, ct);
        return new NaseljeZaTackuDto(z.Id, z.Name);
    }
}
