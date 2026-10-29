using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Catalog;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class PrevodiKatalogaTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    [Fact]
    public async Task Popunjava_samo_prazne_prevode_i_mapa_vodi_srpski_na_engleski()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        var mrkva = await t.Db.Products.SingleAsync(p => p.Slug == "mrkva");
        mrkva.NazivEn = "Admin carrots"; // admin je već unio svoj prevod
        await t.Db.SaveChangesAsync();

        var prvi = await PrevodiKataloga.PopuniAsync(t.Db);
        var drugi = await PrevodiKataloga.PopuniAsync(t.Db);

        prvi.Should().BeGreaterThan(50);
        drugi.Should().Be(0);
        var mapa = await new GetPrevodiHandler(t.Db).Handle(new GetPrevodiQuery(), default);
        mapa["Mrkva"].Should().Be("Admin carrots");
        mapa["Domaći krompir"].Should().Be("White potatoes");
        mapa["Povrće"].Should().Be("Vegetables");
    }
}
