using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Infrastructure.BackgroundJobs;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class GuestOrderTests
{
    [Fact]
    public async Task Zahtjev_se_upisuje_i_ne_pravi_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var mejl = new FakeEmail();

        var rezultat = await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), mejl)
            .Handle(scena.ZahtjevGosta(), default);

        t.Db.Orders.Should().BeEmpty("narudžba nastaje tek na potvrdu");
        var zahtjev = t.Db.GuestOrderRequests.Single();
        zahtjev.Token.Should().HaveLength(32);
        zahtjev.Items.Should().ContainSingle();
        rezultat.ExpiresAt.Should().BeCloseTo(
            zahtjev.CreatedAt.AddMinutes(GuestOrderRequest.TrajanjeMinuta), TimeSpan.FromSeconds(5));
        mejl.ZahtjeviGosta.Should().ContainSingle();
    }

    [Fact]
    public async Task Zahtjev_pamti_cijenu_iz_trenutka_slanja()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        var stavka = t.Db.GuestOrderRequestItems.Single();
        stavka.UnitPrice.Should().Be(20m);
        stavka.ProductName.Should().Be("Med");
    }

    [Fact]
    public async Task Zahtjev_ne_skida_zalihe()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        t.Db.Products.Single().Stock.Should().Be(100, "zalihe se skidaju tek kad narudžba nastane");
    }

    [Fact]
    public async Task Prijavljen_kupac_ne_moze_kroz_zahtjev_gosta()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var kupac = new User { Name = "Mujo", Email = "mujo@test.local", PasswordHash = "x" };
        t.Db.Users.Add(kupac);
        t.Db.SaveChanges();

        var radnja = () => new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(kupac.Id), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        (await radnja.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("already_authenticated");
    }

    [Fact]
    public async Task Zahtjev_ispod_minimuma_pada()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t, minimalniIznos: 500m);

        var radnja = () => new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        (await radnja.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("below_minimum");
    }

    [Fact]
    public async Task Neuspjelo_slanje_mejla_brise_upisan_zahtjev()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var mejl = new FakeEmail { BaciGresku = true };

        var radnja = () => new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), mejl)
            .Handle(scena.ZahtjevGosta(), default);

        await radnja.Should().ThrowAsync<InvalidOperationException>();
        t.Db.GuestOrderRequests.Should().BeEmpty(
            "gost bez uspješno poslanog linka nema kako potvrditi zahtjev, pa red ne smije ostati");
    }

    [Fact]
    public async Task Potvrda_pravi_narudzbu_po_snimljenim_cijenama()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;

        // Cijena poskupi nakon slanja, prije potvrde.
        var proizvod = t.Db.Products.Single();
        proizvod.Price = 99m;
        t.Db.SaveChanges();

        var rezultat = await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        rezultat.Items.Single().UnitPrice.Should().Be(20m, "kupac plaća cijenu koju je vidio");
        rezultat.Code.Should().StartWith("EP-");
        t.Db.GuestOrderRequests.Should().BeEmpty("zahtjev se briše čim narudžba nastane");
    }

    [Fact]
    public async Task Potvrda_dodjeljuje_token_i_prenosi_koordinate_belesku_i_adresu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var napomena = "Zvono ne radi, pozvati po dolasku.";
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(napomena), default);
        var zahtjev = t.Db.GuestOrderRequests.Single();
        var token = zahtjev.Token;

        var rezultat = await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        // Vozačka aplikacija navigira po ovim koordinatama — polovičan ili izgubljen
        // prenos iz zahtjeva u narudžbu bi se otkrio tek na terenu.
        var narudzba = t.Db.Orders.Single(o => o.Id == rezultat.Id);
        narudzba.PublicToken.Should().NotBeNullOrWhiteSpace("i narudžba gosta mora biti sljediva bez naloga");
        narudzba.Latitude.Should().Be(zahtjev.Latitude);
        narudzba.Longitude.Should().Be(zahtjev.Longitude);
        narudzba.Note.Should().Be(napomena);
        narudzba.DeliveryAddress.Should().Be(zahtjev.DeliveryAddress);
    }

    [Fact]
    public async Task Potvrda_skida_zalihe()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;

        await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        t.Db.Products.Single().Stock.Should().Be(99);
    }

    [Fact]
    public async Task Potvrda_istekloga_pada_i_ne_pravi_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var zahtjev = t.Db.GuestOrderRequests.Single();
        zahtjev.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        t.Db.SaveChanges();

        var radnja = () => new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(zahtjev.Token), default);

        (await radnja.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("guest_request_expired");
        t.Db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Dva_klika_na_link_prave_tacno_jednu_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;
        var handler = new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber());

        await handler.Handle(new ConfirmGuestOrderCommand(token), default);
        var drugiKlik = () => handler.Handle(new ConfirmGuestOrderCommand(token), default);

        (await drugiKlik.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("guest_request_expired");
        t.Db.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task Potvrda_pada_ako_je_proizvod_u_medjuvremenu_ugasen()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;
        t.Db.Products.Single().IsAvailable = false;
        t.Db.SaveChanges();

        var radnja = () => new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        var greska = (await radnja.Should().ThrowAsync<DomainException>()).Which;
        greska.Code.Should().Be("product_unavailable");
        greska.Message.Should().Contain("Med", "kupcu treba reći koji proizvod je sporan");
    }

    [Fact]
    public async Task Cistac_brise_samo_istekle()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var handler = new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail());
        await handler.Handle(scena.ZahtjevGosta(), default);
        await handler.Handle(scena.ZahtjevGosta(), default);

        var stari = t.Db.GuestOrderRequests.First();
        stari.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        t.Db.SaveChanges();

        var obrisano = await ExpiredGuestRequestsCleaner.ObrisiIstekleAsync(t.Db, DateTime.UtcNow, default);

        obrisano.Should().Be(1);
        t.Db.GuestOrderRequests.Should().ContainSingle();
        t.Db.GuestOrderRequestItems.Should().ContainSingle("stavke odlaze sa zahtjevom");
    }
}
