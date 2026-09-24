using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
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
}
