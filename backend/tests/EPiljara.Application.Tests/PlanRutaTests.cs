using EPiljara.Application.Features.Admin;

namespace EPiljara.Application.Tests;

/// <summary>Podjela naselja na rute od najviše 10 po lokaciji i redoslijed ruta u danu.</summary>
public class PlanRutaTests
{
    private static Guid Id(int i) => new($"00000000-0000-0000-0000-{i:D12}");

    private static List<TackaNarudzbe> Klasteri()
    {
        // Zapadni klaster (8) oko 17.200 i istočni (7) oko 17.210 — ~800 m razmaka.
        var t = new List<TackaNarudzbe>();
        for (var i = 0; i < 8; i++) t.Add(new(Id(i), 44.770 + i * 0.0003, 17.200 + (i % 3) * 0.0003));
        for (var i = 0; i < 7; i++) t.Add(new(Id(100 + i), 44.770 + i * 0.0003, 17.210 + (i % 3) * 0.0003));
        return t;
    }

    [Fact]
    public void Petnaest_tacaka_Borika_daju_8_i_7_i_zbijene_su()
    {
        var grupe = PlanRuta.Podijeli(Klasteri());

        Assert.Equal(2, grupe.Count);
        Assert.Equal([8, 7], grupe.Select(g => g.Count).OrderByDescending(x => x));
        foreach (var g in grupe)
        {
            var zapadne = g.Count(x => x.Lon < 17.205);
            Assert.True(zapadne == 0 || zapadne == g.Count, "ruta miješa dva kraja naselja");
        }
    }

    [Theory]
    [InlineData(10, new[] { 10 })]
    [InlineData(11, new[] { 6, 5 })]
    [InlineData(20, new[] { 10, 10 })]
    [InlineData(21, new[] { 7, 7, 7 })]
    [InlineData(1, new[] { 1 })]
    public void Granice_10_11_20(int n, int[] velicine)
    {
        var t = Enumerable.Range(0, n).Select(i => new TackaNarudzbe(Id(i), 44.77 + i * 0.001, 17.19)).ToList();

        var grupe = PlanRuta.Podijeli(t);

        Assert.Equal(velicine, grupe.Select(g => g.Count).OrderByDescending(x => x));
        Assert.All(grupe, g => Assert.True(g.Count <= 10));
        Assert.Equal(n, grupe.Sum(g => g.Count));
    }

    [Fact]
    public void Prazan_ulaz_nema_ruta()
    {
        Assert.Empty(PlanRuta.Podijeli([]));
    }

    [Fact]
    public void Isti_ulaz_isti_izlaz()
    {
        var t = Klasteri();
        var izmijesan = t.AsEnumerable().Reverse().ToList();

        var a = PlanRuta.Podijeli(t).Select(g => g.Select(x => x.Id).OrderBy(x => x).ToList()).OrderBy(g => g[0]).ToList();
        var b = PlanRuta.Podijeli(izmijesan).Select(g => g.Select(x => x.Id).OrderBy(x => x).ToList()).OrderBy(g => g[0]).ToList();

        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }

    [Fact]
    public void Bez_koordinata_u_najmanju()
    {
        var t = Klasteri();
        t.Add(new TackaNarudzbe(Id(900), null, null));

        var grupe = PlanRuta.Podijeli(t);

        Assert.Equal([8, 8], grupe.Select(g => g.Count));
        Assert.Contains(grupe, g => g.Any(x => x.Id == Id(900)) && g.Count(x => x.Lat is not null) == 7);
    }

    [Fact]
    public void Samo_bez_koordinata()
    {
        var t = Enumerable.Range(0, 12).Select(i => new TackaNarudzbe(Id(i), null, null)).ToList();

        var grupe = PlanRuta.Podijeli(t);

        Assert.Equal([6, 6], grupe.Select(g => g.Count));
    }

    [Fact]
    public void Redoslijed_ruta_od_magacina()
    {
        // Magacin zapadno; ruta B je bliža magacinu od rute A.
        var a = new RutaTacke(Id(1), [(44.77, 17.25), (44.77, 17.26)]);
        var b = new RutaTacke(Id(2), [(44.77, 17.21), (44.77, 17.22)]);
        var c = new RutaTacke(Id(3), [(44.77, 17.30)]);

        var red = PlanRuta.Redoslijed([a, c, b], 44.77, 17.10);

        Assert.Equal([Id(2), Id(1), Id(3)], red);
    }

    [Fact]
    public void Sljedeca_od_posljednje_stanice()
    {
        // Prva je B (najbliža startu); B završava na istoku, pa je sljedeća C (istok), ne A (blizu starta).
        var a = new RutaTacke(Id(1), [(44.77, 17.12)]);
        var b = new RutaTacke(Id(2), [(44.77, 17.11), (44.77, 17.40)]);
        var c = new RutaTacke(Id(3), [(44.77, 17.41)]);

        var red = PlanRuta.Redoslijed([a, b, c], 44.77, 17.10);

        Assert.Equal([Id(2), Id(3), Id(1)], red);
    }

    [Fact]
    public void Ruta_bez_koordinata_na_kraj()
    {
        var a = new RutaTacke(Id(1), []);
        var b = new RutaTacke(Id(2), [(44.77, 17.21)]);

        Assert.Equal([Id(2), Id(1)], PlanRuta.Redoslijed([a, b], 44.77, 17.10));
    }
}
