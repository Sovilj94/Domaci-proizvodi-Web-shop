using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Auth;
using EPiljara.Application.Features.Poeni;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>P6c Task 2: GET /account/poeni i preporuka pri registraciji.</summary>
public class MojiPoeniTests
{
    private const string Abeceda = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static async Task<User> KorisnikAsync(TestDb t, string ime = "Marko Marković")
    {
        var u = new User { Name = ime, Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x" };
        t.Db.Users.Add(u);
        await t.Db.SaveChangesAsync();
        return u;
    }

    private static Task<MojiPoeniDto> PozoviAsync(TestDb t, Guid? ko) =>
        new GetMojiPoeniHandler(t.Db, new FakeCurrentUser(ko), Options.Create(new PoeniOpcije()))
            .Handle(new GetMojiPoeniQuery(), default);

    [Fact]
    public async Task Stanje_je_zbir_a_vrijednost_je_stanje_puta_cijena_poena()
    {
        using var t = new TestDb();
        var u = await KorisnikAsync(t);
        t.Db.PoeniStavke.AddRange(
            new PoeniStavka { UserId = u.Id, Iznos = 300, Vrsta = VrstaPoena.Isporuka },
            new PoeniStavka { UserId = u.Id, Iznos = -100, Vrsta = VrstaPoena.Iskoristeno });
        await t.Db.SaveChangesAsync();

        var dto = await PozoviAsync(t, u.Id);

        dto.Stanje.Should().Be(200);
        dto.VrijednostKM.Should().Be(10m);
        dto.VrijednostPoenaKM.Should().Be(0.05m);
        dto.NagradaPreporuka.Should().Be(100);
    }

    [Fact]
    public async Task Negativno_stanje_daje_vrijednost_nula()
    {
        using var t = new TestDb();
        var u = await KorisnikAsync(t);
        t.Db.PoeniStavke.Add(new PoeniStavka { UserId = u.Id, Iznos = -40, Vrsta = VrstaPoena.Korekcija });
        await t.Db.SaveChangesAsync();

        var dto = await PozoviAsync(t, u.Id);

        dto.Stanje.Should().Be(-40);
        dto.VrijednostKM.Should().Be(0m);
    }

    [Fact]
    public async Task Kod_se_pravi_pri_prvom_pozivu_a_drugi_vraca_isti()
    {
        using var t = new TestDb();
        var u = await KorisnikAsync(t);

        var prvi = await PozoviAsync(t, u.Id);
        var drugi = await PozoviAsync(t, u.Id);

        prvi.PreporukaKod.Should().HaveLength(8);
        prvi.PreporukaKod.Should().MatchRegex($"^[{Abeceda}]{{8}}$");
        drugi.PreporukaKod.Should().Be(prvi.PreporukaKod);
        t.Db.ChangeTracker.Clear();
        (await t.Db.Users.AsNoTracking().SingleAsync()).PreporukaKod.Should().Be(prvi.PreporukaKod);
    }

    [Fact]
    public async Task Postojeci_kod_se_nikad_ne_prepisuje()
    {
        using var t = new TestDb();
        var u = await KorisnikAsync(t);
        u.PreporukaKod = "ABCD2345";
        await t.Db.SaveChangesAsync();

        var dto = await PozoviAsync(t, u.Id);

        dto.PreporukaKod.Should().Be("ABCD2345");
        t.Db.ChangeTracker.Clear();
        (await t.Db.Users.AsNoTracking().SingleAsync()).PreporukaKod.Should().Be("ABCD2345");
    }

    [Fact]
    public async Task Neprijavljen_ne_moze_da_vidi_poene()
    {
        using var t = new TestDb();

        var radnja = () => PozoviAsync(t, null);

        await radnja.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Istorija_je_najnovija_prva_najvise_50_sa_opisima_po_vrsti()
    {
        using var t = new TestDb();
        var u = await KorisnikAsync(t);
        var preporucen = await KorisnikAsync(t, "marko marković");
        var zona = new DeliveryZone { Name = "Centar", IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        t.Db.AddRange(zona, slot);
        var o1 = new Order
        {
            Code = "EP-1", CustomerName = "x", DeliveryAddress = "y",
            DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id,
        };
        t.Db.Orders.Add(o1);
        var baza = DateTime.UtcNow.AddDays(-10);
        t.Db.PoeniStavke.AddRange(
            new PoeniStavka { UserId = u.Id, Iznos = 20, Vrsta = VrstaPoena.Isporuka, OrderId = o1.Id, Kreirana = baza.AddHours(1) },
            new PoeniStavka { UserId = u.Id, Iznos = -20, Vrsta = VrstaPoena.Iskoristeno, OrderId = o1.Id, Kreirana = baza.AddHours(2) },
            new PoeniStavka { UserId = u.Id, Iznos = 20, Vrsta = VrstaPoena.Povrat, OrderId = o1.Id, Kreirana = baza.AddHours(3) },
            new PoeniStavka { UserId = u.Id, Iznos = 100, Vrsta = VrstaPoena.PreporukaPreporucio, PreporuceniId = preporucen.Id, Kreirana = baza.AddHours(4) },
            new PoeniStavka { UserId = u.Id, Iznos = 100, Vrsta = VrstaPoena.PreporukaNovi, Kreirana = baza.AddHours(5) },
            new PoeniStavka { UserId = u.Id, Iznos = 5, Vrsta = VrstaPoena.Korekcija, Kreirana = baza.AddHours(6) });
        await t.Db.SaveChangesAsync();

        var dto = await PozoviAsync(t, u.Id);

        dto.Istorija.Select(s => s.Opis).Should().Equal(
            "Korekcija",
            "Dobrodošlica — preporuka",
            "Preporuka: Marko M. — prva isporuka stigla",
            "Vraćeno — otkazana EP-1",
            "Iskorišteno na EP-1",
            "Isporuka EP-1");
        dto.Istorija[3].OrderCode.Should().Be("EP-1");
        dto.Istorija[0].OrderCode.Should().BeNull();

        for (var i = 0; i < 60; i++)
            t.Db.PoeniStavke.Add(new PoeniStavka
            { UserId = u.Id, Iznos = 1, Vrsta = VrstaPoena.Korekcija, Kreirana = DateTime.UtcNow.AddMinutes(-i) });
        await t.Db.SaveChangesAsync();
        (await PozoviAsync(t, u.Id)).Istorija.Should().HaveCount(50);
    }

    [Fact]
    public async Task BrojPreporuka_su_korisnici_sa_mojim_id_kao_preporucilac()
    {
        using var t = new TestDb();
        var ja = await KorisnikAsync(t);
        var drugi = await KorisnikAsync(t);
        t.Db.Users.AddRange(
            new User { Name = "A", Email = "a@t.local", PasswordHash = "x", PreporucioId = ja.Id },
            new User { Name = "B", Email = "b@t.local", PasswordHash = "x", PreporucioId = ja.Id },
            new User { Name = "C", Email = "c@t.local", PasswordHash = "x", PreporucioId = drugi.Id });
        await t.Db.SaveChangesAsync();

        (await PozoviAsync(t, ja.Id)).BrojPreporuka.Should().Be(2);
    }

    // ---- registracija ----

    private static RegisterCommand Registracija(string? kod) =>
        new("Novi Kupac", "novi@test.local", "065", "lozinka1", null, null, kod);

    private static Task RegistrujAsync(TestDb t, string? kod) =>
        new RegisterHandler(t.Db, new FakeHasher(), new FakeEmail(), new FakeCurrentUser(null)).Handle(Registracija(kod), default);

    private sealed class FakeHasher : EPiljara.Application.Common.Interfaces.IPasswordHasher
    {
        public string Hash(string password) => "h:" + password;
        public bool Verify(string password, string hash) => hash == "h:" + password;
    }

    private static async Task<User> NoviAsync(TestDb t)
    {
        t.Db.ChangeTracker.Clear();
        return await t.Db.Users.AsNoTracking().SingleAsync(u => u.Email == "novi@test.local");
    }

    [Fact]
    public async Task Vazeci_kod_upisuje_preporucioca()
    {
        using var t = new TestDb();
        var p = await KorisnikAsync(t);
        p.PreporukaKod = "ABCD2345";
        await t.Db.SaveChangesAsync();

        await RegistrujAsync(t, "ABCD2345");

        (await NoviAsync(t)).PreporucioId.Should().Be(p.Id);
    }

    [Fact]
    public async Task Kod_malim_slovima_je_prepoznat()
    {
        using var t = new TestDb();
        var p = await KorisnikAsync(t);
        p.PreporukaKod = "ABCD2345";
        await t.Db.SaveChangesAsync();

        await RegistrujAsync(t, " abcd2345 ");

        (await NoviAsync(t)).PreporucioId.Should().Be(p.Id);
    }

    [Fact]
    public async Task Nepoznat_kod_ne_sprjecava_registraciju()
    {
        using var t = new TestDb();

        await RegistrujAsync(t, "NEPOZNAT");

        (await NoviAsync(t)).PreporucioId.Should().BeNull();
    }

    [Fact]
    public void Kod_sa_razmacima_prolazi_validaciju()
    {
        new RegisterValidator().Validate(Registracija(" ABCD2345 ")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Predugacak_kod_pada_na_validaciji()
    {
        var r = new RegisterValidator().Validate(Registracija("DEVETZNAK"));

        r.Errors.Should().Contain(e => e.ErrorMessage == "Kod preporuke je predugačak.");
    }
}
