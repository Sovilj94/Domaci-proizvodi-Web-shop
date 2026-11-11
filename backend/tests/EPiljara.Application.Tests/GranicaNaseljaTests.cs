using EPiljara.Application.Features.Naselja;
using EPiljara.Domain.Common;

namespace EPiljara.Application.Tests;

public class GranicaNaseljaTests
{
    internal static string Kvadrat(double lon1, double lat1, double lon2, double lat2) =>
        FormattableString.Invariant(
            $$"""{"type":"Polygon","coordinates":[[[{{lon1}},{{lat1}}],[{{lon2}},{{lat1}}],[{{lon2}},{{lat2}}],[{{lon1}},{{lat2}}],[{{lon1}},{{lat1}}]]]}""");

    [Fact]
    public void Tacka_unutra_napolju_i_na_ivici()
    {
        var g = Granica.Procitaj(Kvadrat(17.19, 44.77, 17.20, 44.78));

        Assert.True(Granica.Sadrzi(g, 44.775, 17.195));
        Assert.False(Granica.Sadrzi(g, 44.79, 17.195));
        Assert.True(Granica.Sadrzi(g, 44.77, 17.195));
    }

    [Fact]
    public void MultiPolygon_se_cita()
    {
        const string json = """
        {"type":"MultiPolygon","coordinates":[
          [[[17.0,44.0],[17.1,44.0],[17.1,44.1],[17.0,44.1],[17.0,44.0]]],
          [[[17.5,44.5],[17.6,44.5],[17.6,44.6],[17.5,44.6],[17.5,44.5]]]]}
        """;
        var g = Granica.Procitaj(json);

        Assert.True(Granica.Sadrzi(g, 44.05, 17.05));
        Assert.True(Granica.Sadrzi(g, 44.55, 17.55));
        Assert.False(Granica.Sadrzi(g, 44.3, 17.3));
    }

    [Theory]
    [InlineData("nije json")]
    [InlineData("""{"type":"Point","coordinates":[17.0,44.0]}""")]
    [InlineData("""{"type":"Polygon","coordinates":[[[17.0,44.0],[17.1,44.0]]]}""")]
    public void Neispravan_json_baca_neispravna_granica(string json)
    {
        var ex = Assert.Throws<DomainException>(() => Granica.Procitaj(json));
        Assert.Equal("neispravna_granica", ex.Code);
    }

    [Fact]
    public void Zapis_i_citanje_daju_isti_oblik()
    {
        var g = Granica.Procitaj(Kvadrat(17.19, 44.77, 17.20, 44.78));
        var ponovo = Granica.Procitaj(Granica.Zapisi(g));

        Assert.True(g.EqualsTopologically(ponovo));
    }

    [Fact]
    public void Preklapanje_susjednih_kvadrata_je_nula()
    {
        var a = Granica.Procitaj(Kvadrat(17.19, 44.77, 17.20, 44.78));
        var b = Granica.Procitaj(Kvadrat(17.20, 44.77, 17.21, 44.78));

        Assert.True(Granica.PreklapanjeM2(a, b) < 1);
    }

    [Fact]
    public void Preklapanje_pola_kvadrata()
    {
        var a = Granica.Procitaj(Kvadrat(17.19, 44.77, 17.20, 44.78));
        var b = Granica.Procitaj(Kvadrat(17.195, 44.77, 17.205, 44.78));

        // pola kvadrata: 0.005° dužine × 0.01° širine
        var ocekivano = 0.005 * Math.Cos(44.77 * Math.PI / 180) * 111320 * 0.01 * 110540;
        var stvarno = Granica.PreklapanjeM2(a, b);

        Assert.InRange(stvarno, ocekivano * 0.98, ocekivano * 1.02);
    }

    [Fact]
    public void Centar_je_unutar_granice()
    {
        var g = Granica.Procitaj(Kvadrat(17.19, 44.77, 17.20, 44.78));
        var (lat, lon) = Granica.Centar(g);

        Assert.True(Granica.Sadrzi(g, lat, lon));
    }
}
