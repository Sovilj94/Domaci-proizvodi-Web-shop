using EPiljara.Application.Features.Account;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class ProfileCommandsTests
{
    private static User Korisnik(TestDb t, string email = "mujo@test.local")
    {
        var u = new User
        {
            Name = "Mujo Mujić",
            Email = email,
            Phone = "065111222",
            PasswordHash = "x",
            Role = UserRole.Customer,
        };
        t.Db.Users.Add(u);
        t.Db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Mijenja_ime_i_telefon_i_vraca_azuriran_dto()
    {
        using var t = new TestDb();
        var u = Korisnik(t);

        var dto = await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(u.Id))
            .Handle(new AzurirajProfilCommand("Haso Hasić", "065999888"), default);

        Assert.Equal("Haso Hasić", dto.Name);
        Assert.Equal("065999888", dto.Phone);

        var iz_baze = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal("Haso Hasić", iz_baze.Name);
        Assert.Equal("065999888", iz_baze.Phone);
    }

    [Fact]
    public async Task Ne_dira_email_ni_ulogu()
    {
        using var t = new TestDb();
        var u = Korisnik(t);

        await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(u.Id))
            .Handle(new AzurirajProfilCommand("Haso Hasić", "065999888"), default);

        var iz_baze = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal("mujo@test.local", iz_baze.Email);
        Assert.Equal(UserRole.Customer, iz_baze.Role);
    }

    [Fact]
    public async Task Dira_samo_svog_korisnika()
    {
        using var t = new TestDb();
        var moj = Korisnik(t, "moj@test.local");
        var tudji = Korisnik(t, "tudji@test.local");

        await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(moj.Id))
            .Handle(new AzurirajProfilCommand("Novo Ime", "065000000"), default);

        var drugi = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == tudji.Id);
        Assert.Equal("Mujo Mujić", drugi.Name);
        Assert.Equal("065111222", drugi.Phone);
    }

    [Fact]
    public async Task Nepostojeci_korisnik_baca_not_found()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AzurirajProfilHandler(t.Db, new FakeCurrentUser(Guid.NewGuid()))
                .Handle(new AzurirajProfilCommand("Ime", "065"), default));
    }

    [Theory]
    [InlineData("", "065111222")]
    [InlineData("   ", "065111222")]
    [InlineData("Mujo", "")]
    [InlineData("Mujo", "   ")]
    public void Prazno_ime_ili_telefon_ne_prolaze_validaciju(string ime, string telefon)
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand(ime, telefon));
        Assert.False(rezultat.IsValid);
    }

    [Fact]
    public void Predugo_ime_ne_prolazi_validaciju()
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand(new string('a', 161), "065111222"));
        Assert.False(rezultat.IsValid);
    }

    [Fact]
    public void Ispravan_unos_prolazi_validaciju()
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand("Mujo Mujić", "065111222"));
        Assert.True(rezultat.IsValid);
    }
}
