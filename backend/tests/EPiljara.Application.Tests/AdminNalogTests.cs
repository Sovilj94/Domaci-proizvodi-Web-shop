using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class AdminNalogTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => "h:" + password;
        public bool Verify(string password, string hash) => hash == "h:" + password;
    }

    [Fact]
    public async Task Bez_env_varijabli_ne_radi_nista()
    {
        using var t = new TestDb();

        (await AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), null, "lozinka123", null)).Should().BeFalse();
        (await AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), "a@b.ba", "", null)).Should().BeFalse();
        (await t.Db.Users.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Pravi_potvrdjen_admin_nalog_sa_normalizovanim_emailom()
    {
        using var t = new TestDb();

        await AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), "  Vlasnik@EPiljara.ba ", "tajna-lozinka", "Nikola");

        var u = await t.Db.Users.SingleAsync();
        u.Email.Should().Be("vlasnik@epiljara.ba");
        u.Role.Should().Be(UserRole.Admin);
        u.EmailConfirmed.Should().BeTrue();
        u.PasswordHash.Should().Be("h:tajna-lozinka");
        u.Name.Should().Be("Nikola");
    }

    [Fact]
    public async Task Postojeci_kupac_postaje_admin_i_dobija_novu_lozinku()
    {
        using var t = new TestDb();
        t.Db.Users.Add(new User { Name = "Kupac", Email = "vlasnik@epiljara.ba", PasswordHash = "h:stara", Role = UserRole.Customer });
        await t.Db.SaveChangesAsync();

        await AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), "vlasnik@epiljara.ba", "nova-lozinka", null);
        await AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), "vlasnik@epiljara.ba", "nova-lozinka", null);

        var u = await t.Db.Users.SingleAsync();
        u.Role.Should().Be(UserRole.Admin);
        u.PasswordHash.Should().Be("h:nova-lozinka");
        u.Name.Should().Be("Kupac");
    }

    [Fact]
    public async Task Prekratka_lozinka_se_odbija()
    {
        using var t = new TestDb();

        var act = () => AdminNalog.ObezbijediAsync(t.Db, new LazniHasher(), "a@b.ba", "kratka", null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
