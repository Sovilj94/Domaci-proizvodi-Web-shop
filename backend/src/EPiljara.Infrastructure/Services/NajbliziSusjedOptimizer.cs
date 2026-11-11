using EPiljara.Application.Common.Interfaces;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Rezervni redoslijed kad OSRM nije dostupan (na Renderu ga nema): od polazišta uvijek najbliža
/// preostala stanica po vazdušnoj udaljenosti, pa 2-opt popravka dok ima poboljšanja. Ne zna puteve,
/// pa je dužina samo vazdušna procjena — izvor je "vazdusno" i ruta zato ne postaje Optimized.
/// Zamjenjuje sortiranje po zonama, koje unutar jednog naselja nije davalo nikakav redoslijed.
/// </summary>
public class NajbliziSusjedOptimizer : IRouteOptimizer
{
    private const int MaxProlaza = 50;

    public Task<RouteOptimizationResult> OptimizeAsync(
        double startLat, double startLon, IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
        var sa = stops.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
        var bez = stops.Where(s => !s.Latitude.HasValue || !s.Longitude.HasValue).Select(s => s.OrderId);

        if (sa.Count == 0)
            return Task.FromResult(new RouteOptimizationResult(
                stops.Select(s => s.OrderId).ToList(), null, null, null, IzvoriRedoslijeda.Vazdusno));

        // Najbliži sljedeći. Kod jednake udaljenosti pobjeđuje raniji u ulazu — deterministički.
        var preostale = new List<RouteStopInput>(sa);
        var tura = new List<RouteStopInput>(sa.Count);
        double lat = startLat, lon = startLon;
        while (preostale.Count > 0)
        {
            var i = 0;
            var najbliza = double.MaxValue;
            for (var k = 0; k < preostale.Count; k++)
            {
                var d = Metara(lat, lon, preostale[k].Latitude!.Value, preostale[k].Longitude!.Value);
                if (d < najbliza) { najbliza = d; i = k; }
            }
            tura.Add(preostale[i]);
            (lat, lon) = (preostale[i].Latitude!.Value, preostale[i].Longitude!.Value);
            preostale.RemoveAt(i);
        }

        DvaOpt(startLat, startLon, tura);

        var duzina = Duzina(startLat, startLon, tura);
        return Task.FromResult(new RouteOptimizationResult(
            tura.Select(s => s.OrderId).Concat(bez).ToList(), duzina, null, null, IzvoriRedoslijeda.Vazdusno));
    }

    /// <summary>Okreće dio ture [i..j] kad to skraćuje put. Tura je otvorena (ne vraća se na start),
    /// pa posljednja ivica nema nastavak.</summary>
    private static void DvaOpt(double sLat, double sLon, List<RouteStopInput> t)
    {
        (double, double) P(int k) => k < 0 ? (sLat, sLon) : (t[k].Latitude!.Value, t[k].Longitude!.Value);
        double D((double, double) a, (double, double) b) => Metara(a.Item1, a.Item2, b.Item1, b.Item2);

        for (var prolaz = 0; prolaz < MaxProlaza; prolaz++)
        {
            var bolje = false;
            for (var i = 0; i < t.Count - 1; i++)
            for (var j = i + 1; j < t.Count; j++)
            {
                var prije = D(P(i - 1), P(i)) + (j + 1 < t.Count ? D(P(j), P(j + 1)) : 0);
                var poslije = D(P(i - 1), P(j)) + (j + 1 < t.Count ? D(P(i), P(j + 1)) : 0);
                if (poslije + 0.01 < prije)
                {
                    t.Reverse(i, j - i + 1);
                    bolje = true;
                }
            }
            if (!bolje) break;
        }
    }

    private static double Duzina(double lat, double lon, List<RouteStopInput> t)
    {
        double d = 0;
        foreach (var s in t)
        {
            d += Metara(lat, lon, s.Latitude!.Value, s.Longitude!.Value);
            (lat, lon) = (s.Latitude!.Value, s.Longitude!.Value);
        }
        return d;
    }

    /// <summary>Vazdušna udaljenost (haversine) u metrima.</summary>
    public static double Metara(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
