using EPiljara.Application.Common;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Novosti;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>R1: boja pločice namirnice i prijava na novosti iz footera.</summary>
public class BojaINovostiTests
{
    [Fact]
    public void Zadana_boja_ima_prednost() =>
        BojaProizvoda.Za("#f4c27a", "Krompir").Should().Be("#F4C27A");

    [Fact]
    public void Bez_boje_isti_naziv_daje_istu_boju_iz_palete()
    {
        var a = BojaProizvoda.Za(null, "Crni luk");
        a.Should().Be(BojaProizvoda.Za(null, "Crni luk"));
        BojaProizvoda.Paleta.Should().Contain(a);
        BojaProizvoda.Za("  ", "Crni luk").Should().Be(a);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("123456")]
    [InlineData("#GGGGGG")]
    public void Nevalidna_boja_proizvoda_ne_prolazi(string boja) =>
        new ProductInputValidator().Validate(Proizvod(boja)).IsValid.Should().BeFalse();

    [Fact]
    public void Validna_boja_proizvoda_prolazi() =>
        new ProductInputValidator().Validate(Proizvod("#A1B2C3")).IsValid.Should().BeTrue();

    private static ProductInput Proizvod(string? boja) =>
        new("Krompir", 2m, "kg", 10m, Guid.NewGuid(), Guid.NewGuid(), "", null, true, false, Boja: boja);

    [Fact]
    public async Task Prijava_upisuje_normalizovan_email()
    {
        using var t = new TestDb();

        await new PrijaviNaNovostiHandler(t.Db).Handle(Komanda("  Jelena@Example.com "), default);

        (await t.Db.PrijaveNovosti.SingleAsync()).Email.Should().Be("jelena@example.com");
    }

    [Fact]
    public async Task Ponovna_prijava_ne_pravi_drugi_red()
    {
        using var t = new TestDb();
        var h = new PrijaviNaNovostiHandler(t.Db);

        await h.Handle(Komanda("jelena@example.com"), default);
        await h.Handle(Komanda("JELENA@example.com"), default);

        (await t.Db.PrijaveNovosti.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Honeypot_ne_upisuje()
    {
        using var t = new TestDb();

        await new PrijaviNaNovostiHandler(t.Db).Handle(Komanda("bot@example.com", web: "http://spam"), default);

        (await t.Db.PrijaveNovosti.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("nije-email")]
    [InlineData("")]
    public void Neispravan_email_ne_prolazi(string email) =>
        new PrijaviNaNovostiValidator().Validate(Komanda(email)).IsValid.Should().BeFalse();

    [Fact]
    public void Prazno_tijelo_ne_prolazi() =>
        new PrijaviNaNovostiValidator().Validate(new PrijaviNaNovostiCommand(null!)).IsValid.Should().BeFalse();

    private static PrijaviNaNovostiCommand Komanda(string email, string? web = null) =>
        new(new PrijavaNovostiInput(email, web));
}
