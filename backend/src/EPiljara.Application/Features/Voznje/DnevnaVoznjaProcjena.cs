using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Voznje;

/// <summary>
/// Procjena vozačevog dana: jedna tura od magacina kroz SVE stanice tog dana (sve zone, bez obzira na
/// to na koliko je ruta dan podijeljen) i nazad u magacin, gdje kombi stoji. Rute po zonama postoje zbog
/// ograničenja Google Maps linka; kombi ih vozi jednu za drugom, pa bi zbir krugova po rutama brojao
/// povratak u magacin više puta.
/// </summary>
public static class DnevnaVoznjaProcjena
{
    /// <summary>Stvarni put je duži od vazdušne linije; ~1,3 je uobičajen faktor za grad i prigradska naselja.</summary>
    public const double FaktorPuta = 1.3;
    /// <summary>Prosječna brzina za procjenu trajanja kad OSRM nije dostupan.</summary>
    private const double BrzinaKmh = 40;

    /// <summary>Otisak: magacin + stanice sa koordinatama. Promjena bilo čega od toga mijenja procjenu.</summary>
    public static string Potpis(Magacin magacin, IEnumerable<Order> narudzbe)
    {
        var sb = new StringBuilder();
        sb.Append(Broj(magacin.Latitude)).Append(',').Append(Broj(magacin.Longitude));
        foreach (var o in narudzbe.OrderBy(o => o.Id))
            sb.Append('|').Append(o.Id.ToString("N")).Append(':')
              .Append(o.Latitude is double la ? Broj(la) : "-").Append(',')
              .Append(o.Longitude is double lo ? Broj(lo) : "-");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..32];
    }

    /// <summary>Narudžbe koje kombi obilazi tog dana: sve stanice vozača osim otkazanih.</summary>
    public static List<Order> NarudzbeDana(IEnumerable<DeliveryRoute> rute) =>
        rute.SelectMany(r => r.Stops)
            .Where(s => s.Order is not null && s.Order.Status != OrderStatus.Cancelled)
            .Select(s => s.Order!)
            .DistinctBy(o => o.Id)
            .ToList();

    /// <summary>
    /// Preračunava procjenu ako je skup stanica drugačiji od onog iz kog je izračunata. Red se pravi kad
    /// vozač tog dana ima stanica; red bez stanica i bez unosa goriva se briše. Ne snima — to radi pozivalac.
    /// </summary>
    public static async Task<DnevnaVoznja?> OsvjeziAsync(
        IAppDbContext db, IRouteOptimizer optimizer, Magacin magacin, Guid driverId, DateOnly datum,
        IReadOnlyList<DeliveryRoute> ruteDana, DnevnaVoznja? postojeca, DateTime sada, CancellationToken ct)
    {
        var narudzbe = NarudzbeDana(ruteDana);
        if (narudzbe.Count == 0)
        {
            if (postojeca is null) return null;
            if (postojeca.PlacenoGorivo is null)
            {
                db.DnevneVoznje.Remove(postojeca);
                return null;
            }
        }

        var potpis = Potpis(magacin, narudzbe);
        var voznja = postojeca;
        if (voznja is null)
        {
            voznja = new DnevnaVoznja { DriverId = driverId, Datum = datum };
            db.DnevneVoznje.Add(voznja);
        }
        else if (voznja.ProcjenaPotpis == potpis && voznja.ProcjenaIzracunata is not null)
        {
            return voznja;
        }

        var saKoordinatama = narudzbe.Where(o => o.Latitude.HasValue && o.Longitude.HasValue).ToList();
        voznja.ProcjenaStanica = narudzbe.Count;
        voznja.ProcjenaBezKoordinata = narudzbe.Count - saKoordinatama.Count;
        voznja.ProcjenaPotpis = potpis;
        voznja.ProcjenaIzracunata = sada;

        if (saKoordinatama.Count == 0)
        {
            voznja.ProcjenaMetara = null;
            voznja.ProcjenaSekundi = null;
            voznja.ProcjenaIzvor = null;
            return voznja;
        }

        var ulaz = saKoordinatama.Select(o => new RouteStopInput(
            o.Id, o.DeliveryAddress, o.DeliveryZone?.Name ?? string.Empty, o.Latitude, o.Longitude)).ToList();
        RouteOptimizationResult? rezultat = null;
        try
        {
            rezultat = await optimizer.OptimizeAsync(magacin.Latitude, magacin.Longitude, ulaz, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Procjena nije vrijedna pada ekrana — vazdušna linija ispod.
        }

        if (rezultat?.DuzinaMetara is double metara)
        {
            voznja.ProcjenaMetara = Math.Round(metara);
            voznja.ProcjenaSekundi = rezultat.TrajanjeSekundi;
            voznja.ProcjenaIzvor = "osrm";
        }
        else
        {
            var vazdusno = VazdusnaTura(magacin.Latitude, magacin.Longitude,
                saKoordinatama.Select(o => (o.Latitude!.Value, o.Longitude!.Value)).ToList());
            voznja.ProcjenaMetara = Math.Round(vazdusno * FaktorPuta);
            voznja.ProcjenaSekundi = Math.Round(voznja.ProcjenaMetara.Value / 1000 / BrzinaKmh * 3600);
            voznja.ProcjenaIzvor = "vazdusno";
        }
        return voznja;
    }

    /// <summary>Najbliži susjed od magacina kroz sve tačke pa nazad u magacin, u metrima vazdušne linije.</summary>
    public static double VazdusnaTura(double lat0, double lon0, IReadOnlyList<(double Lat, double Lon)> tacke)
    {
        var preostale = tacke.ToList();
        var (lat, lon) = (lat0, lon0);
        double ukupno = 0;
        while (preostale.Count > 0)
        {
            var i = 0;
            var najbliza = double.MaxValue;
            for (var k = 0; k < preostale.Count; k++)
            {
                var d = Haversine(lat, lon, preostale[k].Lat, preostale[k].Lon);
                if (d < najbliza) { najbliza = d; i = k; }
            }
            ukupno += najbliza;
            (lat, lon) = preostale[i];
            preostale.RemoveAt(i);
        }
        return ukupno + Haversine(lat, lon, lat0, lon0);
    }

    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        static double Rad(double x) => x * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Cijena goriva važeća na dan; null kad nijedna nije unesena.</summary>
    public static CijenaGoriva? CijenaZa(IEnumerable<CijenaGoriva> cijene, DateOnly datum) =>
        cijene.Where(c => c.VaziOd <= datum).OrderBy(c => c.VaziOd).LastOrDefault();

    public static decimal TrosakKm(double km, CijenaGoriva c) =>
        (decimal)km * c.PotrosnjaL100km / 100m * c.CijenaPoLitru;

    /// <summary>Rute (sa stanicama, narudžbama i zonama) za vozače i dane, jednim upitom.</summary>
    public static Task<List<DeliveryRoute>> RuteAsync(IAppDbContext db, DateOnly od, DateOnly doUklj, Guid? driverId, CancellationToken ct) =>
        db.DeliveryRoutes
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.Date >= od && r.Date <= doUklj && (driverId == null || r.DriverId == driverId))
            .ToListAsync(ct);

    private static string Broj(double x) => x.ToString("0.000000", CultureInfo.InvariantCulture);
}
