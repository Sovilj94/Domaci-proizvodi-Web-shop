using EPiljara.Application.Features.Account;
using EPiljara.Application.Features.Auth;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// P7: pismo zahtjeva (`X-Pismo`) se pamti tamo gdje ga kasnije treba mejl ili Viber —
/// na narudžbi, zahtjevu gosta i nalogu — jer se ti šalju i kad zahtjeva više nema.
/// </summary>
public class PismoTests
{
    private static CreateOrderHandler NarudzbaHandler(TestDb t, FakeCurrentUser current) =>
        new(t.Db, current, new FakeEmail(), new FakeViber(),
            Options.Create(TestOpcije.Korpice()), Options.Create(TestOpcije.Pretplate()), Options.Create(TestOpcije.Poeni()));

    [Fact]
    public async Task Narudzba_pamti_cirilicu_iz_zahtjeva()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await NarudzbaHandler(t, new FakeCurrentUser(null, pismo: Pismo.Cirilica)).Handle(scena.Komanda(), default);

        t.Db.Orders.Single().Pismo.Should().Be(Pismo.Cirilica);
    }

    [Fact]
    public async Task Narudzba_bez_zaglavlja_je_latinica()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await scena.Naruci();

        t.Db.Orders.Single().Pismo.Should().Be(Pismo.Latinica);
    }

    [Fact]
    public async Task Pretplatna_narudzba_uzima_pismo_vlasnika_a_ne_zahtjeva()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var vlasnik = new User { Name = "Jelena Petrović", Email = "jelena@test.local", Pismo = Pismo.Cirilica };
        t.Db.Users.Add(vlasnik);
        await t.Db.SaveChangesAsync();

        // Posao pretplata nema HTTP zahtjev: njegov ICurrentUser je uvijek latinica.
        await NarudzbaHandler(t, new FakeCurrentUser(null))
            .Handle(scena.Komanda() with { CustomerId = vlasnik.Id, PretplataId = Guid.NewGuid() }, default);

        t.Db.Orders.Single().Pismo.Should().Be(Pismo.Cirilica);
    }

    [Fact]
    public async Task Gost_na_cirilici_dobija_narudzbu_na_cirilici()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null, pismo: Pismo.Cirilica), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var zahtjev = t.Db.GuestOrderRequests.Single();
        zahtjev.Pismo.Should().Be(Pismo.Cirilica);

        await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber(), Options.Create(TestOpcije.Korpice()))
            .Handle(new ConfirmGuestOrderCommand(zahtjev.Token), default);

        t.Db.Orders.Single().Pismo.Should().Be(Pismo.Cirilica);
    }

    [Fact]
    public async Task Registracija_pamti_pismo_zahtjeva()
    {
        using var t = new TestDb();

        await new RegisterHandler(t.Db, new PismoHasher(), new FakeEmail(), new FakeCurrentUser(null, pismo: Pismo.Cirilica))
            .Handle(new RegisterCommand("Dragan Stojanović", "dragan@test.local", "065", "lozinka1", null, null), default);

        t.Db.Users.Single().Pismo.Should().Be(Pismo.Cirilica);
    }

    [Fact]
    public async Task Kupac_mijenja_pismo_naloga()
    {
        using var t = new TestDb();
        var kupac = new User { Name = "Marko Marković", Email = "marko@test.local" };
        t.Db.Users.Add(kupac);
        await t.Db.SaveChangesAsync();

        var dto = await new PostaviPismoHandler(t.Db, new FakeCurrentUser(kupac.Id))
            .Handle(new PostaviPismoCommand("Cirilica"), default);

        dto.Pismo.Should().Be("Cirilica");
        (await t.Db.Users.SingleAsync()).Pismo.Should().Be(Pismo.Cirilica);
    }

    [Theory]
    [InlineData("Klingonski")]
    [InlineData("")]
    [InlineData("7")]
    public void Nepoznato_pismo_ne_prolazi_validaciju(string pismo) =>
        new PostaviPismoValidator().Validate(new PostaviPismoCommand(pismo)).IsValid.Should().BeFalse();

    [Fact]
    public void Poznato_pismo_prolazi_validaciju() =>
        new PostaviPismoValidator().Validate(new PostaviPismoCommand("Latinica")).IsValid.Should().BeTrue();

    [Fact]
    public async Task Bez_prijave_nema_promjene_pisma()
    {
        using var t = new TestDb();

        var radnja = () => new PostaviPismoHandler(t.Db, new FakeCurrentUser(null))
            .Handle(new PostaviPismoCommand("Cirilica"), default);

        await radnja.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class PismoHasher : Common.Interfaces.IPasswordHasher
    {
        public string Hash(string password) => "h:" + password;
        public bool Verify(string password, string hash) => hash == "h:" + password;
    }
}
