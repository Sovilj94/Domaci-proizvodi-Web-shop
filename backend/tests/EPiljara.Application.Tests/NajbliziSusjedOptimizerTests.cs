using EPiljara.Application.Common.Interfaces;
using EPiljara.Infrastructure.Services;

namespace EPiljara.Application.Tests;

/// <summary>Redoslijed bez OSRM-a (Render): najbliži sljedeći po vazdušnoj udaljenosti, pa 2-opt.</summary>
public class NajbliziSusjedOptimizerTests
{
    private static RouteStopInput S(Guid id, double? lat, double? lon) => new(id, "Adresa", "Borik", lat, lon);

    [Fact]
    public async Task Redoslijed_na_pravoj_liniji()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        // Start na 17.19, tačke istočno: 17.20 (a), 17.21 (b), 17.22 (c) — ulaz izmiješan.
        var r = await new NajbliziSusjedOptimizer().OptimizeAsync(44.77, 17.19,
            [S(c, 44.77, 17.22), S(a, 44.77, 17.20), S(b, 44.77, 17.21)]);

        Assert.Equal([a, b, c], r.Order);
        Assert.Equal("vazdusno", r.Izvor);
        Assert.InRange(r.DuzinaMetara!.Value, 2300, 2450); // 3 × ~790 m
        Assert.Null(r.TrajanjeSekundi);
        Assert.Null(r.Geometrija);
    }

    [Fact]
    public async Task Dva_opt_rasplice_ukrstanje()
    {
        // Četiri ugla pravougaonika; najbliži-susjed od starta pravi ukrštenu turu koju 2-opt raspliće.
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var tacke = new[]
        {
            S(ids[0], 44.770, 17.200), S(ids[1], 44.770, 17.212), S(ids[2], 44.776, 17.212),
            S(ids[3], 44.776, 17.200), S(ids[4], 44.770, 17.206),
        };

        var r = await new NajbliziSusjedOptimizer().OptimizeAsync(44.770, 17.199, tacke);

        var duzinaUkrstene = Duzina(44.770, 17.199, tacke, [ids[0], ids[4], ids[1], ids[3], ids[2]]);
        Assert.True(r.DuzinaMetara <= duzinaUkrstene);
        Assert.Equal(5, r.Order.Distinct().Count());
        // Nema ukrštanja: posljednje dvije stanice su gornji uglovi, susjedne.
        Assert.Equal(new HashSet<Guid> { ids[2], ids[3] }, r.Order.Skip(3).ToHashSet());
    }

    [Fact]
    public async Task Bez_koordinata_na_kraj()
    {
        Guid a = Guid.NewGuid(), bez1 = Guid.NewGuid(), bez2 = Guid.NewGuid();

        var r = await new NajbliziSusjedOptimizer().OptimizeAsync(44.77, 17.19,
            [S(bez1, null, null), S(a, 44.77, 17.20), S(bez2, null, null)]);

        Assert.Equal([a, bez1, bez2], r.Order);
    }

    [Fact]
    public async Task Bez_ijedne_tacke_vraca_ulaz_bez_duzine()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var r = await new NajbliziSusjedOptimizer().OptimizeAsync(44.77, 17.19, [S(a, null, null), S(b, null, null)]);

        Assert.Equal([a, b], r.Order);
        Assert.Null(r.DuzinaMetara);
    }

    private static double Duzina(double lat, double lon, RouteStopInput[] t, Guid[] red)
    {
        double d = 0, pl = lat, po = lon;
        foreach (var id in red)
        {
            var s = t.Single(x => x.OrderId == id);
            d += NajbliziSusjedOptimizer.Metara(pl, po, s.Latitude!.Value, s.Longitude!.Value);
            (pl, po) = (s.Latitude!.Value, s.Longitude!.Value);
        }
        return d;
    }
}
