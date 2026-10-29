using EPiljara.Application.Features.Catalog;
using EPiljara.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Špajz po uzoru na farmit: povrće i voće na kilogram se ne prodaju pojedinačno.</summary>
public class SpajzTests
{
    [Fact]
    public async Task Artikal_van_Spajza_se_ne_vidi_u_katalogu_i_ne_moze_se_naruciti()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var proizvod = await t.Db.Products.SingleAsync();

        (await new GetProductsHandler(t.Db).Handle(new GetProductsQuery(null, null, null, null), default))
            .Should().ContainSingle();

        proizvod.USpajzu = false;
        await t.Db.SaveChangesAsync();

        (await new GetProductsHandler(t.Db).Handle(new GetProductsQuery(null, null, null, null), default))
            .Should().BeEmpty();
        var izuzetak = await FluentActions.Invoking(scena.Naruci).Should().ThrowAsync<DomainException>();
        izuzetak.Which.Code.Should().Be("product_unavailable");
    }

    [Fact]
    public async Task Seed_drzi_povrce_i_voce_van_Spajza_a_med_i_dzem_u_njemu()
    {
        using var t = new TestDb();
        await Infrastructure.Persistence.DbSeeder.SeedAsync(t.Db, new LazniHasher());

        var katalog = await new GetProductsHandler(t.Db).Handle(new GetProductsQuery(null, null, null, null), default);

        katalog.Should().NotContain(p => p.CategorySlug == "povrce" || p.CategorySlug == "voce" || p.CategorySlug == "meso");
        katalog.Should().Contain(p => p.Slug == "livadski-med");
        katalog.Should().Contain(p => p.Slug == "dzem-od-sljive" && p.CategorySlug == "dzem");
        (await t.Db.Categories.AnyAsync(k => k.Slug == "zimnica")).Should().BeFalse();
    }

    private sealed class LazniHasher : Common.Interfaces.IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }
}
