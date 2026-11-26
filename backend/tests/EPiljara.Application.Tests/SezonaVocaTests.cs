using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>Sezona voća je jedno pravilo nad dva mjeseca iz podešavanja (spec 2026-11-26, odluka 2).</summary>
public class SezonaVocaTests
{
    private static KorpiceOpcije O(int? od, int? @do)
    {
        var o = TestOpcije.Korpice();
        o.SezonaVocaOd = od;
        o.SezonaVocaDo = @do;
        return o;
    }

    [Fact]
    public void Bez_podesene_sezone_voce_je_cijele_godine()
    {
        var o = O(null, null);
        for (var m = 1; m <= 12; m++) SezonaVoca.UTijeku(o, m).Should().BeTrue($"mjesec {m}");
    }

    [Fact]
    public void Maj_oktobar()
    {
        var o = O(5, 10);
        SezonaVoca.UTijeku(o, 5).Should().BeTrue();
        SezonaVoca.UTijeku(o, 10).Should().BeTrue();
        SezonaVoca.UTijeku(o, 4).Should().BeFalse();
        SezonaVoca.UTijeku(o, 11).Should().BeFalse();
    }

    [Fact]
    public void Sezona_preko_nove_godine()
    {
        var o = O(11, 3);
        SezonaVoca.UTijeku(o, 12).Should().BeTrue();
        SezonaVoca.UTijeku(o, 1).Should().BeTrue();
        SezonaVoca.UTijeku(o, 4).Should().BeFalse();
    }

    [Fact]
    public void Povrce_nikad_nije_van_sezone()
    {
        SezonaVoca.VanSezone(O(5, 10), TipKorpice.Povrce, 1).Should().BeFalse();
        SezonaVoca.VanSezone(O(5, 10), TipKorpice.Voce, 1).Should().BeTrue();
        SezonaVoca.VanSezone(O(5, 10), TipKorpice.Voce, 6).Should().BeFalse();
    }

    [Fact]
    public void Mjesec_isporuke_ne_danasnji()
    {
        // ISO 2026-W18: ponedjeljak 27. 4., petak 1. 5. — narudžba u aprilu za isporuku u maju.
        SezonaVoca.MjesecIsporuke(2026, 18, DayOfWeek.Friday, "16:00").Should().Be(5);
        SezonaVoca.MjesecIsporuke(2026, 18, DayOfWeek.Monday, "16:00").Should().Be(4);
    }

    [Fact]
    public void Godina_povratka()
    {
        var o = O(5, 10);
        SezonaVoca.GodinaPovratka(o, new DateTime(2026, 12, 4, 16, 0, 0, DateTimeKind.Utc)).Should().Be(2027);
        SezonaVoca.GodinaPovratka(o, new DateTime(2027, 3, 5, 16, 0, 0, DateTimeKind.Utc)).Should().Be(2027);
    }

    [Fact]
    public void Nazivi_mjeseci_u_padezima()
    {
        SezonaVoca.Genitiv(5).Should().Be("maja");
        SezonaVoca.Genitiv(10).Should().Be("oktobra");
        SezonaVoca.Lokativ(5).Should().Be("maju");
    }
}
