using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>Admin panel: pakovanje po kutiji, zone i termini, kupci, sadržaj sajta.</summary>
public class AdminPanelTests
{
    // ---- Pakovanje ----

    [Fact]
    public async Task Pakovanje_razlaze_sedmicu_po_kutiji_sa_sadrzajem_korpice_i_dodacima_iz_spajza()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var korpica = await s.PosaljiKorpicuAsync(1m);
        var dodatak = await s.PosaljiObicanArtikalAsync();

        var dto = await new GetPakovanjeHandler(s.Db).Handle(new GetPakovanjeQuery(s.Godina, s.Sedmica), default);

        var kutija = dto.Narudzbe.Single(n => n.Id == korpica.Id);
        kutija.Korpice.Should().ContainSingle().Which.Sadrzaj.Should().NotBeEmpty();
        kutija.Dodaci.Should().BeEmpty();

        var samoDodatak = dto.Narudzbe.Single(n => n.Id == dodatak.Id);
        samoDodatak.Korpice.Should().BeEmpty();
        samoDodatak.Dodaci.Should().ContainSingle();
        dto.ZbirDodataka.Should().ContainSingle().Which.Naziv.Should().Be(samoDodatak.Dodaci[0].Naziv);
    }

    [Fact]
    public async Task Pakovanje_ne_prikazuje_otkazane_ni_druge_sedmice()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var otkazana = await s.PosaljiKorpicuAsync(1m);
        var red = await s.Db.Orders.FirstAsync(o => o.Id == otkazana.Id);
        red.Status = OrderStatus.Cancelled;
        await s.Db.SaveChangesAsync();

        var ova = await new GetPakovanjeHandler(s.Db).Handle(new GetPakovanjeQuery(s.Godina, s.Sedmica), default);
        var druga = await new GetPakovanjeHandler(s.Db).Handle(new GetPakovanjeQuery(s.Godina, s.Sedmica == 1 ? 2 : s.Sedmica - 1), default);

        ova.Narudzbe.Should().BeEmpty();
        druga.Narudzbe.Should().BeEmpty();
    }

    // ---- Zone i termini ----

    [Fact]
    public async Task Zona_se_pravi_i_mijenja_a_isti_naziv_je_konflikt()
    {
        using var t = new TestDb();
        var nova = await new CreateZonaHandler(t.Db).Handle(new CreateZonaCommand(new ZonaInput(" Borik ", 20m, 3m, true)), default);
        nova.Name.Should().Be("Borik");

        await ((Func<Task>)(() => new CreateZonaHandler(t.Db).Handle(new CreateZonaCommand(new ZonaInput("borik", 0, 0, true)), default)))
            .Should().ThrowAsync<ConflictException>();

        var izmijenjena = await new UpdateZonaHandler(t.Db).Handle(new UpdateZonaCommand(nova.Id, new ZonaInput("Borik", 25m, 4m, false)), default);
        (izmijenjena.MinOrderAmount, izmijenjena.DeliveryFee, izmijenjena.IsActive).Should().Be((25m, 4m, false));
        (await new GetAdminZoneHandler(t.Db).Handle(new GetAdminZoneQuery(), default)).Should().ContainSingle("ugašena zona ostaje u adminu");
    }

    [Theory]
    [InlineData(7, "16:00", "20:00", 48, "dayOfWeek")]
    [InlineData(4, "16", "20:00", 48, "timeFrom")]
    [InlineData(4, "20:00", "16:00", 48, "timeTo")]
    [InlineData(4, "16:00", "20:00", 200, "cutoffHours")]
    public void Termin_validator_odbija_neispravan_unos(int dan, string od, string doo, int rok, string polje)
    {
        var r = new TerminInputValidator().Validate(new TerminInput(dan, od, doo, rok, null));
        r.Errors.Should().Contain(e => e.PropertyName.Equals(polje, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Termin_u_upotrebi_se_ne_brise_a_slobodan_se_brise()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var narudzba = await s.PosaljiKorpicuAsync(1m);
        var zauzet = (await s.Db.Orders.FirstAsync(o => o.Id == narudzba.Id)).DeliverySlotId;
        var slobodan = await new CreateTerminHandler(s.Db).Handle(
            new CreateTerminCommand(new TerminInput(5, "16:00", "20:00", 48, "Petak")), default);

        await ((Func<Task>)(() => new DeleteTerminHandler(s.Db).Handle(new DeleteTerminCommand(zauzet), default)))
            .Should().ThrowAsync<ConflictException>().Where(e => e.Code == "termin_u_upotrebi");
        (await new DeleteTerminHandler(s.Db).Handle(new DeleteTerminCommand(slobodan.Id), default)).Should().Be(slobodan.Id);
    }

    // ---- Kupci ----

    [Fact]
    public async Task Kupci_nose_broj_narudzbi_poene_i_pretragu()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        // Scenario kupcu daje samo Id (narudžba nema FK na Users), pa se red kupca upisuje ovdje.
        s.Db.Users.Add(new User { Id = s.KupacId, Name = "Marko Marković", Email = "marko@test.local", PasswordHash = "x" });
        await s.Db.SaveChangesAsync();
        await s.PosaljiKorpicuAsync(1m);
        s.Db.PoeniStavke.Add(new PoeniStavka { UserId = s.KupacId, Iznos = 40, Vrsta = VrstaPoena.Korekcija });
        s.Db.Users.Add(new User { Name = "Admin", Email = "a@test.local", PasswordHash = "x", Role = UserRole.Admin });
        await s.Db.SaveChangesAsync();

        var svi = await new GetAdminKupciHandler(s.Db).Handle(new GetAdminKupciQuery(null), default);
        var k = svi.Should().ContainSingle("admin nije kupac").Which;
        (k.BrojNarudzbi, k.Poeni).Should().Be((1, 40));

        (await new GetAdminKupciHandler(s.Db).Handle(new GetAdminKupciQuery("nema-takvog"), default)).Should().BeEmpty();

        var detalj = await new GetAdminKupacHandler(s.Db).Handle(new GetAdminKupacQuery(s.KupacId), default);
        detalj.Narudzbe.Should().ContainSingle();
        detalj.Poeni.Should().Be(40);
    }

    // ---- Sadržaj sajta ----

    [Fact]
    public async Task Sadrzaj_sajta_je_prazan_dok_se_ne_sacuva_a_prazno_polje_vraca_podrazumijevano()
    {
        using var t = new TestDb();
        var prije = await new GetSadrzajSajtaHandler(t.Db).Handle(new GetSadrzajSajtaQuery(), default);
        (prije.Telefon, prije.Pitanja).Should().Be((null, null));

        await new SacuvajSadrzajSajtaHandler(t.Db).Handle(new SacuvajSadrzajSajtaCommand(
            " +387 65 123 456 ", "", "Manjača", null,
            [new PitanjeDto { Pitanje = " Šta je korpica? ", Odgovor = "Rok je {rok}." }]), default);

        var poslije = await new GetSadrzajSajtaHandler(t.Db).Handle(new GetSadrzajSajtaQuery(), default);
        poslije.Telefon.Should().Be("+387 65 123 456");
        poslije.Email.Should().BeNull();
        poslije.Pitanja.Should().ContainSingle().Which.Should().Be(new PitanjeDto { Pitanje = "Šta je korpica?", Odgovor = "Rok je {rok}." });

        await new SacuvajSadrzajSajtaHandler(t.Db).Handle(new SacuvajSadrzajSajtaCommand(null, null, null, null, null), default);
        (await t.Db.SadrzajSajta.CountAsync()).Should().Be(1, "uvijek jedan red");
        (await new GetSadrzajSajtaHandler(t.Db).Handle(new GetSadrzajSajtaQuery(), default)).Pitanja.Should().BeNull();
    }

    [Fact]
    public void Sadrzaj_sajta_validator_odbija_prazno_pitanje_i_los_email()
    {
        var r = new SacuvajSadrzajSajtaValidator().Validate(new SacuvajSadrzajSajtaCommand(
            null, "nije-email", null, null, [new PitanjeDto { Pitanje = "", Odgovor = "x" }]));
        r.IsValid.Should().BeFalse();
        r.Errors.Select(e => e.ErrorMessage).Should().Contain(["Upiši ispravan e-mail.", "Pitanje ne smije biti prazno."]);
    }
}
