using EPiljara.Application.Features.Naselja;
using EPiljara.Infrastructure.Persistence.Podaci;

namespace EPiljara.Application.Tests;

/// <summary>Početne granice koje migracija upisuje (tools/naselja → naselja-2026-11-11.json).</summary>
public class PocetnaNaseljaTests
{
    private static readonly IReadOnlyList<PocetnoNaselje> Naselja = PocetnaNaselja.Ucitaj();

    [Fact]
    public void Ima_24_naselja_odluke_vlasnika()
    {
        Assert.Equal(24, Naselja.Count);
        Assert.Contains(Naselja, n => n.Naziv == "Borik");
        Assert.Contains(Naselja, n => n.Naziv == "Drakulić");
        Assert.Contains(Naselja, n => n.Naziv == "Motike");
        Assert.DoesNotContain(Naselja, n => n.Naziv == "Karanovac");
        Assert.DoesNotContain(Naselja, n => n.Naziv == "Bočac");
    }

    [Fact]
    public void Svaki_centar_je_u_svom_naselju()
    {
        foreach (var n in Naselja)
            Assert.True(Granica.Sadrzi(Granica.Procitaj(n.Granica), n.CentarLat, n.CentarLon), n.Naziv);
    }

    [Fact]
    public void Granice_se_ne_preklapaju()
    {
        var g = Naselja.Select(n => Granica.Procitaj(n.Granica)).ToList();
        for (var i = 0; i < g.Count; i++)
            for (var j = i + 1; j < g.Count; j++)
                Assert.True(Granica.PreklapanjeM2(g[i], g[j]) < 1000, $"{Naselja[i].Naziv} / {Naselja[j].Naziv}");
    }

    [Theory]
    [InlineData(44.7722, 17.1910, true)]   // centar grada
    [InlineData(44.7700, 17.2050, true)]   // Borik
    [InlineData(44.6000, 17.0500, false)]  // daleko van grada
    [InlineData(44.8800, 17.3000, false)]  // sjeverno, sela van početnog spiska
    public void Kontrolne_tacke(double lat, double lon, bool ocekivano)
    {
        var pogodak = Naselja.Any(n => Granica.Sadrzi(Granica.Procitaj(n.Granica), lat, lon));
        Assert.Equal(ocekivano, pogodak);
    }

    [Fact]
    public void Sql_ne_pravi_duplikat_i_bjezi_navodnike()
    {
        var sql = PocetnaNaselja.Sql([new PocetnoNaselje("O'Brien", """{"type":"Polygon"}""", 44.1, 17.2)]);

        Assert.Contains("O''Brien", sql);
        Assert.Contains("WHERE NOT EXISTS", sql);
        Assert.Contains("UPDATE \"DeliveryZones\"", sql);
    }
}
