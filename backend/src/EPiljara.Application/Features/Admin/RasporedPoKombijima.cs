using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Prijedlog koja naselja idu u koji kombi. Čista funkcija, bez baze — da bude provjerljiva.
///
/// Metoda je „lepeza" (sweep) oko magacina: naselja se poredaju po uglu gledano iz magacina,
/// počevši poslije najveće praznine među uglovima (da sektor ne presiječe gusto područje), pa se
/// redom pune kombiji. Susjedna naselja tako idu istom vozaču. Koristi se najmanje kombija koliko
/// stane u slobodne kapacitete; opterećenje se ravna na oko ukupno/kombija, a nijedan kombi ne
/// prelazi svoj slobodan kapacitet. Naselje se nikad ne dijeli između kombija — admin dodjeljuje
/// cijela naselja. Udaljenosti su vazdušne (bez OSRM matrice); redoslijed stanica i ruta u
/// kombiju poslije određuje postojeća optimizacija pri dodjeli.
/// </summary>
public static class RasporedPoKombijima
{
    /// <summary>Naselje sa nedodijeljenim narudžbama; tačka je centar naselja (null = magacin).</summary>
    public record Naselje(Guid ZonaId, string Naziv, double? Lat, double? Lon, IReadOnlyList<Guid> OrderIds);

    /// <summary>Aktivan vozač i koliko mu je stanica tog dana još slobodno.</summary>
    public record Vozac(Guid Id, string Naziv, int Slobodno);

    public record Kombi(Guid VozacId, string Naziv, IReadOnlyList<Naselje> Naselja)
    {
        public int Ukupno => Naselja.Sum(n => n.OrderIds.Count);
    }

    public record Rezultat(IReadOnlyList<Kombi> Kombiji, IReadOnlyList<Naselje> Nerasporedjeno);

    public static Rezultat Rasporedi((double Lat, double Lon) magacin, IReadOnlyList<Naselje> naselja, IReadOnlyList<Vozac> vozaci)
    {
        var raspolozivi = vozaci.Where(v => v.Slobodno > 0)
            .OrderByDescending(v => v.Slobodno).ThenBy(v => v.Naziv, StringComparer.Ordinal).ThenBy(v => v.Id)
            .ToList();
        var najveci = raspolozivi.Count == 0 ? 0 : raspolozivi.Max(v => v.Slobodno);
        var nerasporedjeno = naselja.Where(n => n.OrderIds.Count > najveci).ToList();
        var zaRaspored = naselja.Where(n => n.OrderIds.Count > 0 && n.OrderIds.Count <= najveci).ToList();
        if (zaRaspored.Count == 0) return new Rezultat([], nerasporedjeno);

        // Najmanje kombija čiji slobodni kapaciteti zajedno primaju sve.
        var ukupno = zaRaspored.Sum(n => n.OrderIds.Count);
        var k = 0;
        for (var zbir = 0; k < raspolozivi.Count && zbir < ukupno; k++) zbir += raspolozivi[k].Slobodno;
        var kombiji = raspolozivi.Take(k).ToList();
        var cilj = (int)Math.Ceiling(ukupno / (double)kombiji.Count);

        var tovar = kombiji.ToDictionary(v => v.Id, _ => new List<Naselje>());
        int Teret(Vozac v) => tovar[v.Id].Sum(n => n.OrderIds.Count);

        var i = 0;
        foreach (var n in Lepeza(magacin, zaRaspored))
        {
            // Pređi na sljedeći kombi kad je trenutni dostigao cilj ili ovo naselje u njega ne staje.
            while (i < kombiji.Count - 1 && Teret(kombiji[i]) > 0
                   && (Teret(kombiji[i]) >= cilj || Teret(kombiji[i]) + n.OrderIds.Count > kombiji[i].Slobodno))
                i++;
            var v = Teret(kombiji[i]) + n.OrderIds.Count <= kombiji[i].Slobodno
                ? kombiji[i]
                // Posljednji kombi je pun: prvi kombi sa dovoljno mjesta, inače nerasporedjeno.
                : kombiji.OrderByDescending(x => x.Slobodno - Teret(x)).FirstOrDefault(x => Teret(x) + n.OrderIds.Count <= x.Slobodno);
            if (v is null) nerasporedjeno.Add(n);
            else tovar[v.Id].Add(n);
        }

        var rezultat = kombiji
            .Where(v => tovar[v.Id].Count > 0)
            .Select(v => new Kombi(v.Id, v.Naziv,
                tovar[v.Id].OrderBy(n => Udaljenost(magacin, n)).ThenBy(n => n.Naziv, StringComparer.Ordinal).ToList()))
            .ToList();
        return new Rezultat(rezultat, nerasporedjeno);
    }

    /// <summary>Naselja po uglu oko magacina, počevši poslije najveće praznine među uglovima.</summary>
    private static List<Naselje> Lepeza((double Lat, double Lon) m, IReadOnlyList<Naselje> naselja)
    {
        var cos = Math.Cos(m.Lat * Math.PI / 180);
        var poUglu = naselja
            .Select(n => (N: n, Ugao: Ugao(m, n, cos)))
            .OrderBy(x => x.Ugao).ThenBy(x => Udaljenost(m, x.N)).ThenBy(x => x.N.ZonaId)
            .ToList();
        if (poUglu.Count < 2) return poUglu.Select(x => x.N).ToList();

        var pocetak = 0;
        var najvecaPraznina = -1.0;
        for (var j = 0; j < poUglu.Count; j++)
        {
            var sljedeci = poUglu[(j + 1) % poUglu.Count].Ugao + (j + 1 == poUglu.Count ? 360 : 0);
            var praznina = sljedeci - poUglu[j].Ugao;
            if (praznina > najvecaPraznina + 1e-9)
            {
                najvecaPraznina = praznina;
                pocetak = (j + 1) % poUglu.Count;
            }
        }
        return poUglu.Skip(pocetak).Concat(poUglu.Take(pocetak)).Select(x => x.N).ToList();
    }

    private static double Ugao((double Lat, double Lon) m, Naselje n, double cos)
    {
        if (n.Lat is not { } lat || n.Lon is not { } lon) return 0;
        var u = Math.Atan2(lat - m.Lat, (lon - m.Lon) * cos) * 180 / Math.PI;
        return u < 0 ? u + 360 : u;
    }

    private static double Udaljenost((double Lat, double Lon) m, Naselje n) =>
        n.Lat is { } lat && n.Lon is { } lon ? PlanRuta.Metara(m.Lat, m.Lon, lat, lon) : 0;
}

/// <summary>
/// Prijedlog raspodjele za dan isporuke: nedodijeljene narudžbe zadate sedmice čiji je termin
/// baš taj dan (narudžba bez termina ide svaki dan), po naselju, na aktivne vozače sa slobodnim
/// mjestom tog dana. Ništa ne upisuje — admin pregleda, po potrebi prebaci, pa potvrdi grupnom
/// dodjelom po vozaču (koja i optimizuje rute).
/// </summary>
public record PredloziRasporedQuery(string Date, int Godina, int Sedmica) : IRequest<RasporedPoKombijima.Rezultat>;

public class PredloziRasporedHandler(IAppDbContext db)
    : IRequestHandler<PredloziRasporedQuery, RasporedPoKombijima.Rezultat>
{
    public async Task<RasporedPoKombijima.Rezultat> Handle(PredloziRasporedQuery q, CancellationToken ct)
    {
        var datum = Dispecer.ParsirajDatum(q.Date);
        var dan = (int)datum.DayOfWeek;
        var magacin = await db.Magacin.FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Lokacija magacina nije postavljena.", "magacin_nije_postavljen");

        var narudzbe = await db.Orders
                .Where(o => o.Godina == q.Godina && o.Sedmica == q.Sedmica && Dispecer.ZaDostavu.Contains(o.Status))
                .Where(o => o.DeliverySlot == null || o.DeliverySlot.DayOfWeek == dan)
                .Where(o => !db.RouteStops.Any(s => s.OrderId == o.Id && s.Route!.Date == datum))
                .Select(o => new
                {
                    o.Id, o.DeliveryZoneId, Zona = o.DeliveryZone!.Name,
                    o.DeliveryZone.CentarLat, o.DeliveryZone.CentarLon, o.Latitude, o.Longitude, o.CreatedAt,
                }).ToListAsync(ct);

        var naselja = narudzbe
            .GroupBy(o => o.DeliveryZoneId)
            .Select(g =>
            {
                var p = g.First();
                // Centar naselja, a bez njega prosjek tačaka narudžbi.
                var saTackom = g.Where(o => o.Latitude is not null && o.Longitude is not null).ToList();
                var lat = p.CentarLat ?? (saTackom.Count > 0 ? saTackom.Average(o => o.Latitude!.Value) : null);
                var lon = p.CentarLon ?? (saTackom.Count > 0 ? saTackom.Average(o => o.Longitude!.Value) : null);
                return new RasporedPoKombijima.Naselje(g.Key, p.Zona, lat, lon,
                    g.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id).Select(o => o.Id).ToList());
            })
            .ToList();

        var zauzeto = await db.RouteStops.Where(s => s.Route!.Date == datum).Select(s => s.Route!.DriverId).ToListAsync(ct);
        var vozaci = (await db.Drivers.Where(d => d.Aktivan).ToListAsync(ct))
            .Select(d => new RasporedPoKombijima.Vozac(d.Id, d.Name,
                Math.Max(0, Domain.Entities.Driver.MaxStanicaPoDanu - zauzeto.Count(id => id == d.Id))))
            .ToList();

        return RasporedPoKombijima.Rasporedi((magacin.Latitude, magacin.Longitude), naselja, vozaci);
    }
}
