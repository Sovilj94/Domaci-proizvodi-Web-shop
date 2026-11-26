using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>„Javi mi kad stigne voće" (spec 2026-11-26, odluka 5).</summary>
public class PrijaveSezoneTests
{
    /// <summary>Sezona je jedan mjesec koji nije mjesec isporuke sedmice koja se naručuje → voće je van sezone.</summary>
    private static async Task<KorpiceOpcije> VanSezoneAsync(TestDb t, bool uSezoni = false)
    {
        var o = TestOpcije.Korpice();
        var m = await SezonaVoca.MjesecNarudzbeAsync(t.Db, o, default);
        var sezona = uSezoni ? m : m % 12 + 1;
        o.SezonaVocaOd = sezona;
        o.SezonaVocaDo = sezona;
        return o;
    }

    private static Task Prijavi(TestDb t, KorpiceOpcije o, string email, string tip = "voce", string? web = null) =>
        new PrijaviNaSezonuHandler(t.Db, Options.Create(o))
            .Handle(new PrijaviNaSezonuCommand(tip, new PrijavaSezoneInput(email, web)), default);

    [Fact]
    public async Task Prijava_cuva_normalizovan_email()
    {
        using var t = new TestDb();
        var o = await VanSezoneAsync(t);

        await Prijavi(t, o, "  Ana@Primjer.BA ");

        var red = await t.Db.PrijaveSezone.SingleAsync();
        red.Email.Should().Be("ana@primjer.ba");
        red.TipKorpice.Should().Be(TipKorpice.Voce);
        red.ObavijestenAt.Should().BeNull();
    }

    [Fact]
    public async Task Dvostruka_prijava_daje_jedan_red()
    {
        using var t = new TestDb();
        var o = await VanSezoneAsync(t);

        await Prijavi(t, o, "ana@primjer.ba");
        await Prijavi(t, o, "ANA@primjer.ba");

        (await t.Db.PrijaveSezone.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Honeypot_ne_upisuje_nista()
    {
        using var t = new TestDb();
        var o = await VanSezoneAsync(t);

        await Prijavi(t, o, "bot@spam.ba", web: "http://spam");

        (await t.Db.PrijaveSezone.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void Neispravan_email_je_greska_validacije()
    {
        var r = new PrijaviNaSezonuValidator().Validate(
            new PrijaviNaSezonuCommand("voce", new PrijavaSezoneInput("nije-email", null)));

        r.IsValid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.PropertyName == "Input.Email");
        new PrijaviNaSezonuValidator().Validate(new PrijaviNaSezonuCommand("voce", null!)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Prijava_u_sezoni_ili_za_povrce_odbijena()
    {
        using var t = new TestDb();
        var uSezoni = await VanSezoneAsync(t, uSezoni: true);
        var van = await VanSezoneAsync(t);

        (await FluentActions.Awaiting(() => Prijavi(t, uSezoni, "ana@primjer.ba")).Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("prijava_nije_moguca");
        (await FluentActions.Awaiting(() => Prijavi(t, van, "ana@primjer.ba", tip: "povrce")).Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("prijava_nije_moguca");
    }

    [Fact]
    public async Task Obavijest_salje_svima_jednom()
    {
        using var t = new TestDb();
        var o = await VanSezoneAsync(t);
        await Prijavi(t, o, "ana@primjer.ba");
        await Prijavi(t, o, "marko@primjer.ba");
        var mejl = new FakeEmail();
        var h = new ObavijestiPrijaveSezoneHandler(t.Db, mejl, NullLogger<ObavijestiPrijaveSezoneHandler>.Instance);

        var prvi = await h.Handle(new ObavijestiPrijaveSezoneCommand(TipKorpice.Voce), default);
        var drugi = await h.Handle(new ObavijestiPrijaveSezoneCommand(TipKorpice.Voce), default);

        prvi.Should().Be(2);
        drugi.Should().Be(0);
        mejl.SezonaKrenula.Should().BeEquivalentTo(["ana@primjer.ba", "marko@primjer.ba"]);
        (await t.Db.PrijaveSezone.AllAsync(p => p.ObavijestenAt != null)).Should().BeTrue();
    }

    [Fact]
    public async Task Ponovna_prijava_poslije_obavjestenja()
    {
        using var t = new TestDb();
        var o = await VanSezoneAsync(t);
        t.Db.PrijaveSezone.Add(new PrijavaSezone
        {
            Email = "ana@primjer.ba", TipKorpice = TipKorpice.Voce,
            CreatedAt = DateTime.UtcNow.AddYears(-1), ObavijestenAt = DateTime.UtcNow.AddMonths(-6),
        });
        await t.Db.SaveChangesAsync();

        await Prijavi(t, o, "ana@primjer.ba");

        (await t.Db.PrijaveSezone.CountAsync()).Should().Be(2);
    }
}
