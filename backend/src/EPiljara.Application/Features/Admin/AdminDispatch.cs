using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/*
 * Dodjela vozača po JEDNOJ narudžbi, za dispečerski ekran.
 *
 * Zašto ovo postoji pored AssignRouteCommand: taj je deklarativan („ovo su sve narudžbe
 * ovog vozača za ovaj dan") — obriše sve otvorene stanice vozača i sagradi rute iznova, pa
 * na kraju pokrene OSRM za svaku. Za jedan klik „Dodaj" to bi značilo rušenje i ponovnu
 * izgradnju cijelog dana plus onoliko poziva OSRM-u koliko vozač ima ruta, a „Prebaci"
 * bi bio dva odvojena zahtjeva između kojih narudžba visi nedodijeljena.
 *
 * Ovdje je izmjena tačkasta: jedna stanica se pomjeri, redoslijed preostalih se popravi,
 * optimizacija se NE dira. Ruta zato zadržava stare DuzinaMetara/TrajanjeSekundi dok je
 * dispečer ponovo ne optimizuje — što je i pošteno: ta dužina više ne opisuje izmijenjenu
 * rutu, a pisati novu bez OSRM-a značilo bi izmisliti je.
 *
 * Oba handlera učitavaju SVE rute tog dana jednim upitom, mijenjaju ih u memoriji i snimaju
 * jednim SaveChanges. Ne razbijati to na više upita: entitet označen za brisanje je i dalje
 * u bazi dok se ne snimi, pa bi ga ponovni upit vratio u navigacionu kolekciju i brojevi
 * stanica bi ispali pogrešni.
 */

internal static class Dispecer
{
    /// <summary>Statusi u kojima je stanica zatvorena — istorija, ne dira se.</summary>
    internal static readonly OrderStatus[] Zatvoreni =
        [OrderStatus.Delivered, OrderStatus.FailedDelivery, OrderStatus.Cancelled];

    /// <summary>Statusi u kojima narudžba još čeka ili trpi vožnju.</summary>
    internal static readonly OrderStatus[] ZaDostavu =
        [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.OutForDelivery];

    /// <summary>Ista tvrda granica koju drži <see cref="AssignRouteHandler"/> — Google Maps
    /// prima najviše 10 koordinata po linku.</summary>
    internal const int MaxStanicaPoRuti = 10;

    internal const string NazivBezZone = "Bez zone";

    internal static DateOnly ParsirajDatum(string datum) =>
        DateOnly.TryParse(datum, out var d)
            ? d
            : throw new DomainException("Neispravan datum.", "invalid_date");

    /// <summary>Sve rute za dan, sa stanicama, narudžbama i zonama — jedan upit koji oba
    /// handlera koriste kao svoju cijelu radnu sliku.</summary>
    internal static Task<List<DeliveryRoute>> RuteZaDanAsync(
        IAppDbContext db, DateOnly datum, CancellationToken ct) =>
        db.DeliveryRoutes
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.Date == datum)
            .ToListAsync(ct);

    /// <summary>
    /// Popravlja redoslijed stanica u ruti: zatvorene zadržavaju svoj broj (posao koji je vozač
    /// već odradio), otvorene se numerišu redom iznad najvišeg zatvorenog. Bez ovoga brisanje
    /// stanice ostavi rupu u nizu, a dodavanje može ponoviti broj — na RouteStop(RouteId,
    /// Sequence) nema jedinstvenog indeksa, pa se sudar ne bi ni prijavio.
    /// </summary>
    internal static void Prenumerisi(DeliveryRoute ruta)
    {
        var zatvorene = ruta.Stops.Where(s => Zatvoreni.Contains(s.Order!.Status)).ToList();
        var otvorene = ruta.Stops.Except(zatvorene).OrderBy(s => s.Sequence).ToList();

        var sljedeci = zatvorene.Count == 0 ? 1 : zatvorene.Max(s => s.Sequence) + 1;
        foreach (var s in otvorene)
            s.Sequence = sljedeci++;
    }

    /// <summary>
    /// Naziv rute je naziv zone, uz redni broj kad vozač ima više ruta u istoj zoni
    /// ("Centar 1", "Centar 2") — isto pravilo koje <see cref="AssignRouteHandler"/> primjenjuje
    /// pri masovnoj dodjeli. Poziva se poslije svake izmjene da se dvije konvencije ne raziđu.
    /// </summary>
    internal static void PreimenujZonu(
        IEnumerable<DeliveryRoute> ruteVozacaZaDan, Guid? zonaId, string nazivZone)
    {
        var uZoni = ruteVozacaZaDan
            .Where(r => r.DeliveryZoneId == zonaId)
            .OrderBy(r => r.Id)
            .ToList();

        for (var i = 0; i < uZoni.Count; i++)
            uZoni[i].Naziv = uZoni.Count > 1 ? $"{nazivZone} {i + 1}" : nazivZone;
    }

    /// <summary>Skida stanicu sa rute i pospremi za sobom: prazna ruta se briše, preostale
    /// dobijaju popravljen redoslijed i nazive.</summary>
    internal static void SkiniStanicu(
        IAppDbContext db, List<DeliveryRoute> sveRute, DeliveryRoute ruta, RouteStop stanica)
    {
        ruta.Stops.Remove(stanica);
        db.RouteStops.Remove(stanica);

        var vozacevRute = sveRute.Where(r => r.DriverId == ruta.DriverId).ToList();

        if (ruta.Stops.Count == 0)
        {
            db.DeliveryRoutes.Remove(ruta);
            sveRute.Remove(ruta);
            vozacevRute.Remove(ruta);
        }
        else
        {
            Prenumerisi(ruta);
        }

        PreimenujZonu(
            vozacevRute, ruta.DeliveryZoneId, ruta.DeliveryZone?.Name ?? NazivBezZone);
    }

    /// <summary>
    /// Lijepi narudžbu na vozačevu rutu za taj dan: traži rutu iste zone koja još ima mjesta,
    /// inače pravi novu, pa popravlja redoslijed i nazive.
    /// </summary>
    /// <remarks>
    /// Namjerno NE provjerava kapacitet i NE snima. Pojedinačna dodjela broji jedno mjesto po
    /// pozivu, a grupna sva unaprijed da bi mogla odbiti cijelo naselje odjednom — da provjera
    /// stoji ovdje, grupna bi je radila po narudžbi i pukla na sredini naselja.
    /// </remarks>
    internal static void DodijeliNaRutu(
        IAppDbContext db, List<DeliveryRoute> sveRute, Order narudzba, Guid vozacId, DateOnly datum)
    {
        var vozacevRute = sveRute.Where(r => r.DriverId == vozacId).ToList();

        var zonaId = narudzba.DeliveryZoneId;
        var nazivZone = narudzba.DeliveryZone?.Name ?? NazivBezZone;

        // Ruta iste zone koja još ima mjesta; inače nova. Zona odlučuje jer vozač obilazi
        // naselje po naselje, a granica od 10 dolazi od Google Maps linka.
        var ruta = vozacevRute.FirstOrDefault(
            r => r.DeliveryZoneId == zonaId && r.Stops.Count < MaxStanicaPoRuti);

        if (ruta is null)
        {
            ruta = new DeliveryRoute
            {
                Date = datum,
                DriverId = vozacId,
                DeliveryZoneId = zonaId,
                Naziv = nazivZone,
                Status = RouteStatus.Planned,
            };
            db.DeliveryRoutes.Add(ruta);
            sveRute.Add(ruta);
            vozacevRute.Add(ruta);
        }

        var stanica = new RouteStop
        {
            OrderId = narudzba.Id,
            Order = narudzba,
            Sequence = ruta.Stops.Count == 0 ? 1 : ruta.Stops.Max(s => s.Sequence) + 1,
        };

        // Eksplicitan Add je obavezan, nije stilska sitnica. `Entity` popunjava Id sa
        // Guid.NewGuid() još u inicijalizatoru, a EF za Guid ključeve po konvenciji uzima
        // ValueGeneratedOnAdd. Kad DetectChanges naiđe na nepraćenu stanicu u kolekciji već
        // praćene rute, stanje bira po ključu: ključ je popunjen → Modified → UPDATE reda koji
        // ne postoji → DbUpdateConcurrencyException („0 rows affected"). Kod NOVE rute to ne
        // pukne jer db.DeliveryRoutes.Add kaskadno označi cijeli graf kao Added — zato je
        // greška pogađala samo dodavanje u rutu koja već postoji u bazi.
        ruta.Stops.Add(stanica);
        db.RouteStops.Add(stanica);

        Prenumerisi(ruta);
        PreimenujZonu(vozacevRute, zonaId, nazivZone);
    }

    /// <summary>Stanice vozača za taj dan, po rutama — zajednički odgovor oba handlera.</summary>
    internal static IReadOnlyList<DeliveryRouteDto> VozacevePoRutama(
        IEnumerable<DeliveryRoute> sveRute, Guid driverId) =>
        sveRute.Where(r => r.DriverId == driverId).Select(r => r.ToDto()).ToList();
}

/* ---------- Dodjela i prebacivanje ---------- */

/// <summary>
/// Dodjeljuje narudžbu vozaču za dati dan. Isti poziv radi i prebacivanje: ako narudžba već
/// stoji na nečijoj ruti tog dana, stanica se skida odande i pravi kod novog vozača.
/// </summary>
public record AssignOrderDriverCommand(Guid OrderId, Guid DriverId, string Date)
    : IRequest<IReadOnlyList<DeliveryRouteDto>>;

public class AssignOrderDriverHandler(IAppDbContext db)
    : IRequestHandler<AssignOrderDriverCommand, IReadOnlyList<DeliveryRouteDto>>
{
    public async Task<IReadOnlyList<DeliveryRouteDto>> Handle(
        AssignOrderDriverCommand cmd, CancellationToken ct)
    {
        var datum = Dispecer.ParsirajDatum(cmd.Date);

        var narudzba = await db.Orders
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrderId, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        // Pokriva i zatvorene (Delivered/FailedDelivery/Cancelled) i sve buduće statuse koji
        // se ne voze — zato provjera stoji na "šta se smije", a ne na "šta se ne smije".
        if (!Dispecer.ZaDostavu.Contains(narudzba.Status))
            throw new DomainException(
                "Narudžba je zatvorena i ne može se dodijeliti vozaču.", "order_closed");

        var vozac = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.DriverId, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        if (!vozac.Aktivan)
            throw new DomainException("Vozač je neaktivan.", "driver_inactive");

        var sveRute = await Dispecer.RuteZaDanAsync(db, datum, ct);

        // Postojeća stanica te narudžbe tog dana, kod bilo kog vozača — prebacivanje.
        var staraRuta = sveRute.FirstOrDefault(r => r.Stops.Any(s => s.OrderId == narudzba.Id));
        if (staraRuta is not null)
        {
            if (staraRuta.DriverId == vozac.Id)
                // Već je kod ovog vozača. Nije greška — dispečer je dva puta kliknuo isto.
                return Dispecer.VozacevePoRutama(sveRute, vozac.Id);

            Dispecer.SkiniStanicu(
                db, sveRute, staraRuta, staraRuta.Stops.First(s => s.OrderId == narudzba.Id));
        }

        var zauzeto = sveRute.Where(r => r.DriverId == vozac.Id).Sum(r => r.Stops.Count);
        if (zauzeto >= Domain.Entities.Driver.MaxStanicaPoDanu)
            throw new DomainException(
                $"Vozač je popunjen — {Domain.Entities.Driver.MaxStanicaPoDanu} stanica je dnevni maksimum.",
                "driver_full");

        Dispecer.DodijeliNaRutu(db, sveRute, narudzba, vozac.Id, datum);

        await db.SaveChangesAsync(ct);

        return Dispecer.VozacevePoRutama(sveRute, vozac.Id);
    }
}

/* ---------- Grupna dodjela (cijelo naselje jednom vozaču) ---------- */

/// <summary>
/// Dodjeljuje više narudžbi istom vozaču u jednom potezu — dispečer šalje cijelo naselje.
/// Radi i prebacivanje: narudžba koja stoji kod drugog vozača se skida odande.
/// </summary>
/// <remarks>
/// Sve ili ništa. Kapacitet se provjerava unaprijed nad cijelim skupom, pa naselje ili prođe
/// cijelo ili nijedna narudžba ne mrdne — djelimična dodjela bi ostavila dispečera da gata
/// šta je prošlo a šta nije.
/// </remarks>
public record AssignOrdersBulkCommand(IReadOnlyList<Guid> OrderIds, Guid DriverId, string Date)
    : IRequest<IReadOnlyList<DeliveryRouteDto>>;

public class AssignOrdersBulkHandler(IAppDbContext db)
    : IRequestHandler<AssignOrdersBulkCommand, IReadOnlyList<DeliveryRouteDto>>
{
    public async Task<IReadOnlyList<DeliveryRouteDto>> Handle(
        AssignOrdersBulkCommand cmd, CancellationToken ct)
    {
        var datum = Dispecer.ParsirajDatum(cmd.Date);

        var trazeni = cmd.OrderIds.Distinct().ToList();
        if (trazeni.Count == 0)
            throw new DomainException("Nije izabrana nijedna narudžba.", "no_orders");

        var vozac = await db.Drivers.FirstOrDefaultAsync(d => d.Id == cmd.DriverId, ct)
            ?? throw new NotFoundException("Vozač nije pronađen.");

        if (!vozac.Aktivan)
            throw new DomainException("Vozač je neaktivan.", "driver_inactive");

        var narudzbe = await db.Orders
            .Include(o => o.DeliveryZone)
            .Where(o => trazeni.Contains(o.Id))
            .ToListAsync(ct);

        if (narudzbe.Count != trazeni.Count)
            throw new NotFoundException("Neka od narudžbi nije pronađena.");

        if (narudzbe.FirstOrDefault(o => !Dispecer.ZaDostavu.Contains(o.Status)) is { } zatvorena)
            throw new DomainException(
                $"Narudžba {zatvorena.Code} je zatvorena i ne može se dodijeliti vozaču.",
                "order_closed");

        var sveRute = await Dispecer.RuteZaDanAsync(db, datum, ct);

        // Ono što je već kod ovog vozača ne troši novo mjesto — dispečer je kliknuo „sve" nad
        // naseljem u kojem mu je dio ranije dodijeljen. Bez ovoga bi provjera kapaciteta
        // brojala iste narudžbe dvaput i odbila dodjelu koja zapravo staje.
        var vecKodNjega = sveRute
            .Where(r => r.DriverId == vozac.Id)
            .SelectMany(r => r.Stops)
            .Select(s => s.OrderId)
            .ToHashSet();

        var zaDodavanje = narudzbe.Where(o => !vecKodNjega.Contains(o.Id)).ToList();
        if (zaDodavanje.Count == 0)
            return Dispecer.VozacevePoRutama(sveRute, vozac.Id);

        var zauzeto = sveRute.Where(r => r.DriverId == vozac.Id).Sum(r => r.Stops.Count);
        var slobodno = Math.Max(0, Domain.Entities.Driver.MaxStanicaPoDanu - zauzeto);

        if (zaDodavanje.Count > slobodno)
            throw new DomainException(
                $"{vozac.Name} ima {slobodno} slobodnih mjesta, a dodjeljuje se {zaDodavanje.Count} narudžbi.",
                "driver_full");

        foreach (var narudzba in zaDodavanje)
        {
            // Prebacivanje: stanica kod drugog vozača se prvo skida, pa lijepi ovdje.
            var staraRuta = sveRute.FirstOrDefault(r => r.Stops.Any(s => s.OrderId == narudzba.Id));
            if (staraRuta is not null)
                Dispecer.SkiniStanicu(
                    db, sveRute, staraRuta, staraRuta.Stops.First(s => s.OrderId == narudzba.Id));

            Dispecer.DodijeliNaRutu(db, sveRute, narudzba, vozac.Id, datum);
        }

        await db.SaveChangesAsync(ct);

        return Dispecer.VozacevePoRutama(sveRute, vozac.Id);
    }
}

/* ---------- Uklanjanje dodjele ---------- */

/// <summary>Skida narudžbu sa rute za dati dan; narudžba se vraća u „Nedodijeljeno".</summary>
/// <remarks><c>MediatR.Unit</c> je pun naziv namjerno — ovaj fajl koristi
/// <c>EPiljara.Domain.Enums</c> zbog <c>OrderStatus</c>, a tamo živi i <c>Unit</c>
/// (jedinica mjere proizvoda), pa je goli <c>Unit</c> dvosmislen.</remarks>
public record RemoveOrderDriverCommand(Guid OrderId, string Date) : IRequest<MediatR.Unit>;

public class RemoveOrderDriverHandler(IAppDbContext db)
    : IRequestHandler<RemoveOrderDriverCommand, MediatR.Unit>
{
    public async Task<MediatR.Unit> Handle(RemoveOrderDriverCommand cmd, CancellationToken ct)
    {
        var datum = Dispecer.ParsirajDatum(cmd.Date);
        var sveRute = await Dispecer.RuteZaDanAsync(db, datum, ct);

        var ruta = sveRute.FirstOrDefault(r => r.Stops.Any(s => s.OrderId == cmd.OrderId))
            ?? throw new NotFoundException("Narudžba nije dodijeljena vozaču za taj dan.");

        var stanica = ruta.Stops.First(s => s.OrderId == cmd.OrderId);

        if (Dispecer.Zatvoreni.Contains(stanica.Order!.Status))
            throw new DomainException(
                "Narudžba je već zatvorena i ne može se skinuti sa rute.", "stop_closed");

        Dispecer.SkiniStanicu(db, sveRute, ruta, stanica);

        await db.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
