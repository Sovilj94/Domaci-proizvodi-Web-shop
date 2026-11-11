using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Unit = MediatR.Unit;

namespace EPiljara.Application.Features.Naselja;

/*
 * Admin stranica „Naselja" (zamjenjuje stare zone): granica, centar, aktivnost i provjera granice.
 *
 * Kad se granica ili aktivnost promijeni, naselje se ponovo računa za sačuvane adrese, pretplate i
 * otvorene narudžbe koje još nisu na ruti. Prelaze samo one čija tačka padne u neko drugo aktivno
 * naselje; tačka koja ne padne nigdje zadržava staro naselje i vodi se kao „van granica" (adresa
 * tada pri sljedećoj narudžbi dobija grešku). Cijena narudžbe se nikad ne preračunava — snimak je.
 */

public record NaseljeAdminDto(
    Guid Id, string Name, bool IsActive, string? Granica, double? CentarLat, double? CentarLon,
    string IzvorGranice, int OtvorenihNarudzbi, int VanGranica, bool ImaNarudzbi);

public record UticajDto(int Narudzbe, int Adrese, int Pretplate);

/// <summary>Sacuvano = false znači da izmjena pomjera naselje postojećim zapisima, pa admin
/// prvo vidi <see cref="Uticaj"/> i potvrđuje (PotvrdiUticaj = true).</summary>
public record SacuvajNaseljeRezultat(bool Sacuvano, UticajDto? Uticaj, NaseljeAdminDto? Naselje);

internal static class NaseljaPravila
{
    /// <summary>Preklapanje ispod ovoga je šum crtanja (zajednička ivica nacrtana rukom).</summary>
    public const double ZanemarljivoPreklapanjeM2 = 1000;

    public static readonly OrderStatus[] Otvoreni =
        [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.OutForDelivery];

    /// <summary>Statusi u kojima narudžba smije promijeniti naselje (još se ne vozi).</summary>
    public static readonly OrderStatus[] PrijeVoznje =
        [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Packing];

    /// <summary>Aktivna naselja sa granicom, po imenu — isti redoslijed kao OdredjivanjeNaselja.</summary>
    public static List<(Guid Id, Geometry G)> Aktivna(IEnumerable<DeliveryZone> zone) =>
        zone.Where(z => z.IsActive && z.Granica != null)
            .OrderBy(z => z.Name, StringComparer.Ordinal)
            .Select(z => (z.Id, G: Procitaj(z.Granica!)))
            .Where(x => x.G is not null)
            .Select(x => (x.Id, x.G!))
            .ToList();

    public static Guid? Pogodi(List<(Guid Id, Geometry G)> aktivna, double? lat, double? lon) =>
        lat is null || lon is null
            ? null
            : aktivna.Where(a => Granica.Sadrzi(a.G, lat.Value, lon.Value)).Select(a => (Guid?)a.Id).FirstOrDefault();

    private static Geometry? Procitaj(string g)
    {
        try { return Granica.Procitaj(g); } catch (DomainException) { return null; }
    }

    /// <summary>Zapisi čije bi se naselje promijenilo pod datim skupom naselja. Primijeni = true
    /// ih i mijenja (bez snimanja).</summary>
    public static async Task<UticajDto> PreracunajAsync(
        IAppDbContext db, IReadOnlyList<DeliveryZone> zone, bool primijeni, CancellationToken ct)
    {
        var aktivna = Aktivna(zone);

        var naRuti = db.RouteStops.Select(s => s.OrderId);
        var narudzbe = await db.Orders
            .Where(o => PrijeVoznje.Contains(o.Status) && o.Latitude != null && !naRuti.Contains(o.Id))
            .ToListAsync(ct);
        var adrese = await db.Addresses.Where(a => a.Latitude != null).ToListAsync(ct);
        var pretplate = await db.Pretplate
            .Where(p => p.Status != StatusPretplate.Otkazana && p.Latitude != null).ToListAsync(ct);

        int n = 0, a = 0, p = 0;
        foreach (var o in narudzbe)
            if (Pogodi(aktivna, o.Latitude, o.Longitude) is { } z && z != o.DeliveryZoneId)
            { n++; if (primijeni) o.DeliveryZoneId = z; }
        foreach (var x in adrese)
            if (Pogodi(aktivna, x.Latitude, x.Longitude) is { } z && z != x.DeliveryZoneId)
            { a++; if (primijeni) x.DeliveryZoneId = z; }
        foreach (var x in pretplate)
            if (Pogodi(aktivna, x.Latitude, x.Longitude) is { } z && z != x.DeliveryZoneId)
            { p++; if (primijeni) x.DeliveryZoneId = z; }

        return new UticajDto(n, a, p);
    }
}

/* ---------- spisak ---------- */

public record GetAdminNaseljaQuery : IRequest<IReadOnlyList<NaseljeAdminDto>>;

public class GetAdminNaseljaHandler(IAppDbContext db) : IRequestHandler<GetAdminNaseljaQuery, IReadOnlyList<NaseljeAdminDto>>
{
    public async Task<IReadOnlyList<NaseljeAdminDto>> Handle(GetAdminNaseljaQuery q, CancellationToken ct)
    {
        var zone = await db.DeliveryZones.AsNoTracking().OrderBy(z => z.Name).ToListAsync(ct);

        var otvorene = await db.Orders.AsNoTracking()
            .Where(o => NaseljaPravila.Otvoreni.Contains(o.Status))
            .Select(o => new { o.DeliveryZoneId, o.Latitude, o.Longitude })
            .ToListAsync(ct);
        var adrese = await db.Addresses.AsNoTracking()
            .Where(a => a.DeliveryZoneId != null)
            .Select(a => new { DeliveryZoneId = a.DeliveryZoneId!.Value, a.Latitude, a.Longitude })
            .ToListAsync(ct);
        var saNarudzbama = (await db.Orders.AsNoTracking().Select(o => o.DeliveryZoneId).Distinct().ToListAsync(ct))
            .ToHashSet();

        return zone.Select(z =>
        {
            var g = z.Granica is null ? null : TryProcitaj(z.Granica);
            bool Van(double? lat, double? lon) =>
                !z.IsActive || g is null || lat is null || lon is null || !Granica.Sadrzi(g, lat.Value, lon.Value);

            var mojeNarudzbe = otvorene.Where(o => o.DeliveryZoneId == z.Id).ToList();
            var van = mojeNarudzbe.Count(o => Van(o.Latitude, o.Longitude))
                      + adrese.Where(a => a.DeliveryZoneId == z.Id).Count(a => Van(a.Latitude, a.Longitude));

            return new NaseljeAdminDto(z.Id, z.Name, z.IsActive, z.Granica, z.CentarLat, z.CentarLon,
                z.IzvorGranice, mojeNarudzbe.Count, van, saNarudzbama.Contains(z.Id));
        }).ToList();
    }

    private static Geometry? TryProcitaj(string g)
    {
        try { return Granica.Procitaj(g); } catch (DomainException) { return null; }
    }
}

/* ---------- novo / izmjena ---------- */

public record SacuvajNaseljeCommand(
    Guid? Id, string Name, bool IsActive, string? Granica, double? CentarLat, double? CentarLon,
    bool Potvrdjeno, bool PotvrdiUticaj) : IRequest<SacuvajNaseljeRezultat>;

public class SacuvajNaseljeValidator : AbstractValidator<SacuvajNaseljeCommand>
{
    public SacuvajNaseljeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Upiši naziv naselja.")
            .MaximumLength(80).WithMessage("Naziv može imati najviše 80 znakova.");
        RuleFor(x => x.CentarLat).InclusiveBetween(-90, 90).When(x => x.CentarLat.HasValue)
            .WithMessage("Neispravan centar.");
        RuleFor(x => x.CentarLon).InclusiveBetween(-180, 180).When(x => x.CentarLon.HasValue)
            .WithMessage("Neispravan centar.");
    }
}

public class SacuvajNaseljeHandler(IAppDbContext db) : IRequestHandler<SacuvajNaseljeCommand, SacuvajNaseljeRezultat>
{
    public async Task<SacuvajNaseljeRezultat> Handle(SacuvajNaseljeCommand cmd, CancellationToken ct)
    {
        var sve = await db.DeliveryZones.ToListAsync(ct);
        var naziv = cmd.Name.Trim();

        DeliveryZone z;
        if (cmd.Id is { } id)
            z = sve.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("Naselje nije pronađeno.");
        else
        {
            z = new DeliveryZone();
            db.DeliveryZones.Add(z);
            sve.Add(z);
        }

        if (sve.Any(x => x != z && string.Equals(x.Name.Trim(), naziv, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Naselje sa ovim nazivom već postoji.", "naselje_postoji");

        Geometry? g = cmd.Granica is null ? null : Granica.Procitaj(cmd.Granica);
        if (g is not null)
            foreach (var drugo in sve.Where(x => x != z && x.Granica != null))
            {
                Geometry dg;
                try { dg = Granica.Procitaj(drugo.Granica!); } catch (DomainException) { continue; }
                if (Granica.PreklapanjeM2(g, dg) > NaseljaPravila.ZanemarljivoPreklapanjeM2)
                    throw new DomainException(
                        $"Granica se preklapa sa naseljem {drugo.Name}.", "naselja_se_preklapaju");
            }

        var mijenjaPripadnost = z.Granica != cmd.Granica || z.IsActive != cmd.IsActive || cmd.Id is null;

        z.Name = naziv;
        z.IsActive = cmd.IsActive;
        z.Granica = g is null ? null : Granica.Zapisi(g);
        if (cmd.CentarLat is { } la && cmd.CentarLon is { } lo)
            (z.CentarLat, z.CentarLon) = (la, lo);
        else if (g is not null)
            (z.CentarLat, z.CentarLon) = Granica.Centar(g);
        z.IzvorGranice = cmd.Potvrdjeno ? IzvoriGranice.Potvrdjeno : IzvoriGranice.Procjena;

        if (mijenjaPripadnost)
        {
            var uticaj = await NaseljaPravila.PreracunajAsync(db, sve, primijeni: cmd.PotvrdiUticaj, ct);
            if (!cmd.PotvrdiUticaj && uticaj is not { Narudzbe: 0, Adrese: 0, Pretplate: 0 })
            {
                // Ništa se ne snima: izmjene na praćenom entitetu se odbacuju zajedno sa kontekstom zahtjeva.
                return new SacuvajNaseljeRezultat(false, uticaj, null);
            }
        }

        await db.SaveChangesAsync(ct);

        var dto = (await new GetAdminNaseljaHandler(db).Handle(new GetAdminNaseljaQuery(), ct)).Single(x => x.Id == z.Id);
        return new SacuvajNaseljeRezultat(true, null, dto);
    }
}

/* ---------- brisanje ---------- */

public record ObrisiNaseljeCommand(Guid Id) : IRequest<Unit>;

public class ObrisiNaseljeHandler(IAppDbContext db) : IRequestHandler<ObrisiNaseljeCommand, Unit>
{
    public async Task<Unit> Handle(ObrisiNaseljeCommand cmd, CancellationToken ct)
    {
        var z = await db.DeliveryZones.FirstOrDefaultAsync(x => x.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Naselje nije pronađeno.");

        var koristi = await db.Orders.AnyAsync(o => o.DeliveryZoneId == z.Id, ct)
                      || await db.GuestOrderRequests.AnyAsync(r => r.DeliveryZoneId == z.Id, ct)
                      || await db.Pretplate.AnyAsync(p => p.DeliveryZoneId == z.Id, ct)
                      || await db.DeliveryRoutes.AnyAsync(r => r.DeliveryZoneId == z.Id, ct);
        if (koristi)
            throw new ConflictException(
                "Naselje ima narudžbe ili pretplate i ne može se obrisati. Isključi ga.", "naselje_ima_narudzbe");

        // Sačuvane adrese gube naselje (kolona je nullable); dobiće ga ponovo pri sljedećem čuvanju.
        foreach (var a in await db.Addresses.Where(a => a.DeliveryZoneId == z.Id).ToListAsync(ct))
            a.DeliveryZoneId = null;

        db.DeliveryZones.Remove(z);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
