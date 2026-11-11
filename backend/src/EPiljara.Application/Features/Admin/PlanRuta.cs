namespace EPiljara.Application.Features.Admin;

public record TackaNarudzbe(Guid Id, double? Lat, double? Lon);

/// <summary>Stanice rute redom (po Sequence) — prva i posljednja određuju redoslijed ruta.</summary>
public record RutaTacke(Guid Id, IReadOnlyList<(double Lat, double Lon)> Stanice);

/// <summary>
/// Čiste funkcije planiranja dana, bez baze:
/// <list type="bullet">
/// <item><see cref="Podijeli"/> — naselje sa više od 10 narudžbi na rute po lokaciji, da bliske adrese
/// idu zajedno (ranije se sjeklo po vremenu naručivanja, pa su se obje rute razvlačile preko naselja);</item>
/// <item><see cref="Redoslijed"/> — koja ruta ide prva: najbliža startu, pa svaka sljedeća najbliža
/// posljednjoj stanici prethodne.</item>
/// </list>
/// </summary>
public static class PlanRuta
{
    public const int MaxPoRuti = Dispecer.MaxStanicaPoRuti;

    private static readonly double Kx = Math.Cos(44.77 * Math.PI / 180);

    /// <summary>
    /// Dijeli na <c>ceil(n / max)</c> ruta ravnomjernih veličina (11 → 6 + 5). Tačke sa koordinatama
    /// se dijele rekurzivnom bisekcijom po osi većeg raspona; tačke bez koordinata (stare narudžbe)
    /// idu poslije u rutu sa najmanje stanica. Isti skup daje istu podjelu bez obzira na redoslijed ulaza.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TackaNarudzbe>> Podijeli(IReadOnlyList<TackaNarudzbe> tacke, int max = MaxPoRuti)
    {
        if (tacke.Count == 0) return [];
        var k = (tacke.Count + max - 1) / max;

        var sa = tacke.Where(t => t.Lat is not null && t.Lon is not null).OrderBy(t => t.Id).ToList();
        var bez = tacke.Where(t => t.Lat is null || t.Lon is null).OrderBy(t => t.Id).ToList();

        // k je izračunat nad svim tačkama, pa ima mjesta i za one bez koordinata: dopunjavaju
        // najmanje rute i nijedna ne prelazi ceil(n / k) <= max.
        var grupe = new List<List<TackaNarudzbe>>();
        Bisekcija(sa, k, grupe);
        while (grupe.Count < k) grupe.Add([]);

        foreach (var t in bez)
            grupe.OrderBy(g => g.Count).ThenBy(g => grupe.IndexOf(g)).First().Add(t);

        return grupe.Where(g => g.Count > 0).Select(g => (IReadOnlyList<TackaNarudzbe>)g).ToList();
    }

    private static void Bisekcija(List<TackaNarudzbe> t, int k, List<List<TackaNarudzbe>> izlaz)
    {
        if (k <= 1 || t.Count <= 1)
        {
            izlaz.Add(t);
            return;
        }

        var xs = t.Select(p => p.Lon!.Value * Kx).ToList();
        var ys = t.Select(p => p.Lat!.Value).ToList();
        var poX = xs.Max() - xs.Min() >= ys.Max() - ys.Min();
        var sortirane = t
            .OrderBy(p => poX ? p.Lon!.Value : p.Lat!.Value)
            .ThenBy(p => p.Id)
            .ToList();

        var k1 = k / 2;
        var n1 = (int)Math.Round((double)t.Count * k1 / k, MidpointRounding.AwayFromZero);
        Bisekcija(sortirane.Take(n1).ToList(), k1, izlaz);
        Bisekcija(sortirane.Skip(n1).ToList(), k - k1, izlaz);
    }

    public static IReadOnlyList<Guid> Redoslijed(IReadOnlyList<RutaTacke> rute, double startLat, double startLon)
    {
        var preostale = rute.Where(r => r.Stanice.Count > 0).OrderBy(r => r.Id).ToList();
        var red = new List<Guid>(rute.Count);
        double lat = startLat, lon = startLon;

        while (preostale.Count > 0)
        {
            var sljedeca = Najbliza(preostale, lat, lon);
            red.Add(sljedeca.Id);
            (lat, lon) = sljedeca.Stanice[^1];
            preostale.Remove(sljedeca);
        }

        red.AddRange(rute.Where(r => r.Stanice.Count == 0).OrderBy(r => r.Id).Select(r => r.Id));
        return red;
    }

    /// <summary>Ruta čija je najbliža stanica najbliža tački; kod jednake udaljenosti manji Id.</summary>
    public static RutaTacke Najbliza(IEnumerable<RutaTacke> rute, double lat, double lon) =>
        rute.Where(r => r.Stanice.Count > 0)
            .OrderBy(r => r.Stanice.Min(s => Metara(lat, lon, s.Lat, s.Lon)))
            .ThenBy(r => r.Id)
            .First();

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
