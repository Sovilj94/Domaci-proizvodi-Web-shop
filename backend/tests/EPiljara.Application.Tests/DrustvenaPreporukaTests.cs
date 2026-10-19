using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Auth;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>B5: kod preporuke iz linka radi i kad se novi kupac registruje preko Google/Facebook prijave.</summary>
public class DrustvenaPreporukaTests
{
    private sealed class Verifikator(string email) : ISocialAuthVerifier
    {
        public Task<ExternalUser> VerifyGoogleAsync(string idToken, CancellationToken ct = default) =>
            Task.FromResult(new ExternalUser(email, "Nikola Petrović"));
        public Task<ExternalUser> VerifyFacebookAsync(string accessToken, CancellationToken ct = default) =>
            Task.FromResult(new ExternalUser(email, "Nikola Petrović"));
    }

    private sealed class Jwt : IJwtTokenService
    {
        public string CreateToken(User user) => "token";
    }

    private static async Task<User> PreporucilacAsync(TestDb t)
    {
        var u = new User { Name = "Jelena Petrović", Email = "jelena@test.local", PreporukaKod = "ABCD2345" };
        t.Db.Users.Add(u);
        await t.Db.SaveChangesAsync();
        return u;
    }

    [Fact]
    public async Task Nov_nalog_preko_googlea_pamti_preporucioca()
    {
        using var t = new TestDb();
        var jelena = await PreporucilacAsync(t);

        await new GoogleLoginHandler(t.Db, new Verifikator("nikola@test.local"), new Jwt(), new FakeCurrentUser(null))
            .Handle(new GoogleLoginCommand("x", " abcd2345 "), default);

        (await t.Db.Users.SingleAsync(u => u.Email == "nikola@test.local")).PreporucioId.Should().Be(jelena.Id);
    }

    [Fact]
    public async Task Postojeci_nalog_ne_dobija_preporucioca_naknadno()
    {
        using var t = new TestDb();
        await PreporucilacAsync(t);
        t.Db.Users.Add(new User { Name = "Nikola", Email = "nikola@test.local" });
        await t.Db.SaveChangesAsync();

        await new FacebookLoginHandler(t.Db, new Verifikator("nikola@test.local"), new Jwt(), new FakeCurrentUser(null))
            .Handle(new FacebookLoginCommand("x", "ABCD2345"), default);

        (await t.Db.Users.SingleAsync(u => u.Email == "nikola@test.local")).PreporucioId.Should().BeNull();
    }

    [Fact]
    public async Task Nepoznat_kod_ne_sprjecava_prijavu()
    {
        using var t = new TestDb();

        var r = await new GoogleLoginHandler(t.Db, new Verifikator("nikola@test.local"), new Jwt(), new FakeCurrentUser(null))
            .Handle(new GoogleLoginCommand("x", "NEPOSTOJ"), default);

        r.Token.Should().Be("token");
        (await t.Db.Users.SingleAsync()).PreporucioId.Should().BeNull();
    }
}
