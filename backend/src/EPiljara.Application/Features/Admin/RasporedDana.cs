using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin;

/// <summary>
/// Zajednička logika oba ekrana za dodjelu (/admin/dostava i /admin/routes): podjela naselja po
/// lokaciji, redoslijed stanica i redni brojevi ruta u danu vozača.
/// </summary>
internal static class RasporedDana
{
    /// <summary>Ruta je počela kad je vozač zatvorio bar jednu stanicu. Takva ruta se ne rastura:
    /// stanice iz nje ne prelaze u druge rute, a njen redni broj ostaje.</summary>
    public static bool JePocela(DeliveryRoute r) =>
        r.Stops.Any(s => s.Order is not null && Dispecer.Zatvoreni.Contains(s.Order.Status));

    /// <summary>
    /// Redni brojevi ruta jednog vozača za dan. Počete i završene zadržavaju broj (stara bez broja
    /// dobija prvi iza najvećeg). Nepočete idu iza njih po <see cref="PlanRuta.Redoslijed"/>, od
    /// posljednje zatvorene stanice dana ili od magacina.
    /// </summary>
    public static void Prenumerisi(IReadOnlyList<DeliveryRoute> ruteVozaca, double magLat, double magLon)
    {
        var pocete = ruteVozaca.Where(JePocela)
            .OrderBy(r => r.RedniBroj ?? int.MaxValue).ThenBy(r => r.Naziv).ThenBy(r => r.Id)
            .ToList();

        var max = pocete.Where(r => r.RedniBroj.HasValue).Select(r => r.RedniBroj!.Value).DefaultIfEmpty(0).Max();
        foreach (var r in pocete.Where(r => r.RedniBroj is null))
            r.RedniBroj = ++max;

        double lat = magLat, lon = magLon;
        var zadnjaZatvorena = pocete
            .OrderByDescending(r => r.RedniBroj)
            .SelectMany(r => r.Stops
                .Where(s => Dispecer.Zatvoreni.Contains(s.Order!.Status) && s.Order.Latitude.HasValue && s.Order.Longitude.HasValue)
                .OrderByDescending(s => s.Sequence))
            .FirstOrDefault();
        if (zadnjaZatvorena is not null)
            (lat, lon) = (zadnjaZatvorena.Order!.Latitude!.Value, zadnjaZatvorena.Order.Longitude!.Value);

        var nepocete = ruteVozaca.Where(r => !JePocela(r)).ToList();
        foreach (var id in PlanRuta.Redoslijed(nepocete.Select(Tacke).ToList(), lat, lon))
            nepocete.First(r => r.Id == id).RedniBroj = ++max;
    }

    /// <summary>Stanice rute redom, samo one sa tačkom.</summary>
    public static RutaTacke Tacke(DeliveryRoute r) => new(r.Id, r.Stops
        .OrderBy(s => s.Sequence)
        .Where(s => s.Order?.Latitude is not null && s.Order.Longitude is not null)
        .Select(s => (s.Order!.Latitude!.Value, s.Order.Longitude!.Value))
        .ToList());

    public static IReadOnlyList<List<Order>> Podijeli(IReadOnlyList<Order> narudzbe) =>
        PlanRuta.Podijeli(narudzbe.Select(o => new TackaNarudzbe(o.Id, o.Latitude, o.Longitude)).ToList())
            .Select(g => g.Select(t => narudzbe.First(o => o.Id == t.Id)).ToList())
            .ToList();

    /// <summary>
    /// Grupna dodjela jednog naselja: nepočete rute vozača u tom naselju se rasturaju i njihove
    /// narudžbe se zajedno sa novim dijele iznova po lokaciji. Počete rute se ne diraju.
    /// Vraća nove rute (za optimizaciju).
    /// </summary>
    public static List<DeliveryRoute> PrerasporediZonu(
        IAppDbContext db, List<DeliveryRoute> sveRute, Guid vozacId, DateOnly datum,
        Guid zonaId, string nazivZone, IReadOnlyList<Order> nove)
    {
        var nepocete = sveRute
            .Where(r => r.DriverId == vozacId && r.DeliveryZoneId == zonaId && !JePocela(r))
            .ToList();

        var narudzbe = nepocete.SelectMany(r => r.Stops.Select(s => s.Order!))
            .Concat(nove)
            .DistinctBy(o => o.Id)
            .ToList();

        foreach (var r in nepocete)
        {
            foreach (var s in r.Stops.ToList()) db.RouteStops.Remove(s);
            db.DeliveryRoutes.Remove(r);
            sveRute.Remove(r);
        }

        var napravljene = new List<DeliveryRoute>();
        foreach (var grupa in Podijeli(narudzbe))
        {
            var ruta = new DeliveryRoute
            {
                Date = datum, DriverId = vozacId, DeliveryZoneId = zonaId, Naziv = nazivZone,
                Status = Domain.Enums.RouteStatus.Planned,
            };
            var i = 0;
            foreach (var o in grupa)
                ruta.Stops.Add(new RouteStop { RouteId = ruta.Id, OrderId = o.Id, Order = o, Sequence = ++i });
            db.DeliveryRoutes.Add(ruta);
            sveRute.Add(ruta);
            napravljene.Add(ruta);
        }

        Dispecer.PreimenujZonu(sveRute.Where(r => r.DriverId == vozacId), zonaId, nazivZone);
        return napravljene;
    }

    /// <summary>Optimizuje izmijenjene rute od magacina i prenumeriše sve rute pogođenih vozača.</summary>
    public static async Task ZavrsiAsync(
        IAppDbContext db, List<DeliveryRoute> sveRute, IEnumerable<DeliveryRoute> izmijenjene,
        IEnumerable<Guid> vozaci, IRouteOptimizer optimizer, CancellationToken ct)
    {
        var magacin = await MagacinCitac.UcitajAsync(db, ct);
        foreach (var r in izmijenjene.Distinct().Where(sveRute.Contains))
            await Optimizacija.PrimijeniAsync(r, magacin.Latitude, magacin.Longitude, optimizer, ct);
        foreach (var v in vozaci.Distinct())
            NumerisiIImenuj(sveRute.Where(r => r.DriverId == v).ToList(), magacin.Latitude, magacin.Longitude);
    }

    /// <summary>Redni brojevi, pa nazivi po njima („Borik 1" je ruta koja se vozi prva).</summary>
    public static void NumerisiIImenuj(IReadOnlyList<DeliveryRoute> ruteVozaca, double magLat, double magLon)
    {
        Prenumerisi(ruteVozaca, magLat, magLon);
        foreach (var zona in ruteVozaca.GroupBy(r => r.DeliveryZoneId))
            Dispecer.PreimenujZonu(ruteVozaca, zona.Key, zona.First().DeliveryZone?.Name
                ?? zona.SelectMany(r => r.Stops).Select(s => s.Order?.DeliveryZone?.Name).FirstOrDefault(n => n is not null)
                ?? Dispecer.NazivBezZone);
    }
}

/* ---------- Pregled podjele prije potvrde ---------- */

public record PodjelaStavkaDto(string Naziv, int Broj);

/// <summary>Kako bi grupna dodjela podijelila izabrane narudžbe („Borik 1: 8 · Borik 2: 7"),
/// bez ikakvog upisa.</summary>
public record PregledPodjeleQuery(IReadOnlyList<Guid> OrderIds, Guid DriverId, string Date)
    : IRequest<IReadOnlyList<PodjelaStavkaDto>>;

public class PregledPodjeleHandler(IAppDbContext db) : IRequestHandler<PregledPodjeleQuery, IReadOnlyList<PodjelaStavkaDto>>
{
    public async Task<IReadOnlyList<PodjelaStavkaDto>> Handle(PregledPodjeleQuery q, CancellationToken ct)
    {
        var datum = Dispecer.ParsirajDatum(q.Date);
        var trazeni = q.OrderIds.Distinct().ToList();
        var narudzbe = await db.Orders.AsNoTracking().Include(o => o.DeliveryZone)
            .Where(o => trazeni.Contains(o.Id)).ToListAsync(ct);
        var rute = await db.DeliveryRoutes.AsNoTracking()
            .Include(r => r.Stops).ThenInclude(s => s.Order)
            .Where(r => r.Date == datum && r.DriverId == q.DriverId)
            .ToListAsync(ct);

        var rezultat = new List<PodjelaStavkaDto>();
        foreach (var zona in narudzbe.GroupBy(o => o.DeliveryZoneId).OrderBy(g => g.First().DeliveryZone?.Name))
        {
            var naziv = zona.First().DeliveryZone?.Name ?? Dispecer.NazivBezZone;
            var uZoni = rute.Where(r => r.DeliveryZoneId == zona.Key).ToList();
            var pocetih = uZoni.Count(RasporedDana.JePocela);
            var vecNaRutama = uZoni.Where(RasporedDana.JePocela).SelectMany(r => r.Stops).Select(s => s.OrderId).ToHashSet();
            var skup = uZoni.Where(r => !RasporedDana.JePocela(r)).SelectMany(r => r.Stops.Select(s => s.Order!))
                .Concat(zona.Where(o => !vecNaRutama.Contains(o.Id)))
                .DistinctBy(o => o.Id)
                .ToList();

            var grupe = RasporedDana.Podijeli(skup);
            var ukupno = pocetih + grupe.Count;
            for (var i = 0; i < grupe.Count; i++)
                rezultat.Add(new PodjelaStavkaDto(ukupno > 1 ? $"{naziv} {pocetih + i + 1}" : naziv, grupe[i].Count));
        }
        return rezultat;
    }
}
