using System.Text.Json;
using EPiljara.Application.Common;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>
/// Isti vektori kao `frontend/src/lib/pismo/preslovi.test.ts` — fajl je ugovor između
/// dvije implementacije istih pravila. Ako ovdje padne a tamo prođe, implementacije su se razišle.
/// </summary>
public class CirilicaTests
{
    private record Vektor(string Lat, string Cir, bool Povratno);

    public static IEnumerable<object[]> Vektori()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "pismo-vektori.json"));
        var lista = JsonSerializer.Deserialize<List<Vektor>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return lista.Select(v => new object[] { v.Lat, v.Cir });
    }

    [Theory]
    [MemberData(nameof(Vektori))]
    public void UCirilicu_po_vektorima(string lat, string cir) =>
        Cirilica.UCirilicu(lat).Should().Be(cir);

    [Theory]
    [MemberData(nameof(Vektori))]
    public void UCirilicu_je_idempotentno(string _, string cir) =>
        Cirilica.UCirilicu(cir).Should().Be(cir);

    [Fact]
    public void Html_tagovi_atributi_i_entiteti_ostaju()
    {
        const string html = "<a href=\"https://epiljara.ba/narudzba/abc\" style=\"color:#123\">Prati narudžbu</a> &amp; hvala&nbsp;ti";

        Cirilica.UCirilicuHtml(html).Should().Be(
            "<a href=\"https://epiljara.ba/narudzba/abc\" style=\"color:#123\">Прати наруџбу</a> &amp; хвала&nbsp;ти");
    }

    [Fact]
    public void Html_style_i_komentar_ostaju()
    {
        const string html = "<style>body { font-family: Arial; }</style><!-- napomena --><p>Dobar dan</p>";

        Cirilica.UCirilicuHtml(html).Should().Be(
            "<style>body { font-family: Arial; }</style><!-- napomena --><p>Добар дан</p>");
    }

    [Fact]
    public void Prazan_i_null_tekst_prolaze()
    {
        Cirilica.UCirilicu("").Should().Be("");
        Cirilica.UCirilicuHtml("").Should().Be("");
    }
}
