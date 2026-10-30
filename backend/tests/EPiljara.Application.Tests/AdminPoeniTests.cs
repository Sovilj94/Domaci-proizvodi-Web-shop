using EPiljara.Application.Features.Poeni;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>B4: admin ekran poena — lista sa stanjem, istorija kupca i ručna korekcija.</summary>
public class AdminPoeniTests
{
    private static async Task<(User Kupac, User Admin)> SpremiAsync(TestDb t, int pocetno = 0)
    {
        var kupac = new User { Name = "Marko Marković", Email = "marko@test.local", PasswordHash = "x" };
        var admin = new User { Name = "Jovana Admin", Email = "admin@test.local", PasswordHash = "x", Role = UserRole.Admin };
        t.Db.Users.AddRange(kupac, admin);
        if (pocetno != 0)
            t.Db.PoeniStavke.Add(new PoeniStavka { UserId = kupac.Id, Iznos = pocetno, Vrsta = VrstaPoena.Isporuka });
        await t.Db.SaveChangesAsync();
        return (kupac, admin);
    }

    private static KorigujPoeneHandler Korekcija(TestDb t, User admin) =>
        new(t.Db, new FakeCurrentUser(admin.Id, UserRole.Admin));

    [Fact]
    public async Task Korekcija_dodaje_stavku_sa_razlogom_i_autorom_a_stanje_je_novi_zbir()
    {
        using var t = new TestDb();
        var (kupac, admin) = await SpremiAsync(t, pocetno: 100);

        var dto = await Korekcija(t, admin).Handle(new KorigujPoeneCommand(kupac.Id, 50, "  Žalba na kasnu isporuku "), default);

        dto.Stanje.Should().Be(150);
        var prva = dto.Istorija.First();
        prva.Vrsta.Should().Be("Korekcija");
        prva.Opis.Should().Be("Korekcija: Žalba na kasnu isporuku");
        prva.Unio.Should().Be("Jovana Admin");
        var red = await t.Db.PoeniStavke.AsNoTracking().SingleAsync(s => s.Vrsta == VrstaPoena.Korekcija);
        red.UnioId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Korekcija_ne_smije_spustiti_stanje_ispod_nule()
    {
        using var t = new TestDb();
        var (kupac, admin) = await SpremiAsync(t, pocetno: 30);

        var poziv = () => Korekcija(t, admin).Handle(new KorigujPoeneCommand(kupac.Id, -31, "Greška"), default);

        (await poziv.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("poeni_ispod_nule");
        (await PoeniPravila.StanjeAsync(t.Db, kupac.Id, default)).Should().Be(30);
    }

    [Fact]
    public async Task Korekcija_do_tacno_nule_prolazi()
    {
        using var t = new TestDb();
        var (kupac, admin) = await SpremiAsync(t, pocetno: 30);

        var dto = await Korekcija(t, admin).Handle(new KorigujPoeneCommand(kupac.Id, -30, "Poništeno"), default);

        dto.Stanje.Should().Be(0);
    }

    [Fact]
    public async Task Korekcija_adminu_ili_nepostojecem_korisniku_je_404()
    {
        using var t = new TestDb();
        var (_, admin) = await SpremiAsync(t);

        await ((Func<Task>)(() => Korekcija(t, admin).Handle(new KorigujPoeneCommand(admin.Id, 10, "x"), default)))
            .Should().ThrowAsync<NotFoundException>();
        await ((Func<Task>)(() => Korekcija(t, admin).Handle(new KorigujPoeneCommand(Guid.NewGuid(), 10, "x"), default)))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(0, "razlog", "iznos")]
    [InlineData(100_001, "razlog", "iznos")]
    [InlineData(10, "", "razlog")]
    public void Validator_odbija_nulu_preveliku_korekciju_i_prazan_razlog(int iznos, string razlog, string polje)
    {
        var rezultat = new KorigujPoeneValidator().Validate(new KorigujPoeneCommand(Guid.NewGuid(), iznos, razlog));

        rezultat.IsValid.Should().BeFalse();
        rezultat.Errors.Should().Contain(e => e.PropertyName.Equals(polje, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Lista_vraca_samo_kupce_sa_stanjem_i_trazi_po_imenu_ili_emailu()
    {
        using var t = new TestDb();
        var (kupac, _) = await SpremiAsync(t, pocetno: 70);
        t.Db.Users.Add(new User { Name = "Petar Petrović", Email = "petar@test.local", PasswordHash = "x" });
        await t.Db.SaveChangesAsync();

        var sve = await new GetAdminPoeniKupciHandler(t.Db).Handle(new GetAdminPoeniKupciQuery(null), default);
        sve.Select(k => k.Ime).Should().Equal("Marko Marković", "Petar Petrović"); // admin nije na listi; veće stanje prvo
        sve[0].Stanje.Should().Be(70);
        sve[1].Stanje.Should().Be(0);

        var nadjeni = await new GetAdminPoeniKupciHandler(t.Db).Handle(new GetAdminPoeniKupciQuery("PETAR@"), default);
        nadjeni.Should().ContainSingle().Which.Ime.Should().Be("Petar Petrović");
    }

    [Fact]
    public async Task Kupac_vidi_razlog_korekcije_u_svojoj_istoriji()
    {
        using var t = new TestDb();
        var (kupac, admin) = await SpremiAsync(t);
        await Korekcija(t, admin).Handle(new KorigujPoeneCommand(kupac.Id, 20, "Poklon za strpljenje"), default);

        var moji = await new GetMojiPoeniHandler(t.Db, new FakeCurrentUser(kupac.Id),
                Microsoft.Extensions.Options.Options.Create(TestOpcije.Poeni()))
            .Handle(new GetMojiPoeniQuery(), default);

        moji.Istorija.Should().ContainSingle().Which.Opis.Should().Be("Korekcija: Poklon za strpljenje");
    }
}
