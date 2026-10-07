using EPiljara.Application.Features.Catalog;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>
/// Javni katalog (GetProductsQuery/GetProductBySlugQuery) ne smije prikazivati korpice —
/// one su Product red sa Vrsta == Korpica, zaseban od običnih artikala, i imaju svoj
/// katalog pod /korpice (KorpiceQueries.cs). Seeder korpicu vezuje za prvu kategoriju i
/// prvog proizvođača samo da zadovolji FK, pa bi se bez ovog filtera pojavila u javnom
/// katalogu kao obično povrće od nasumičnog proizvođača.
/// </summary>
public class CatalogQueriesTests
{
    private static (Product obican, Product korpica) Postavi(TestDb t)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Porodica Kovačević", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        var obican = new Product
        {
            Name = "Babura paprika", Slug = "babura-paprika", Price = 3m,
            Unit = Unit.Kg, CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 20,
            Vrsta = VrstaProizvoda.Obican,
        };
        var korpica = new Product
        {
            Name = "Mala domaćinska korpica", Slug = "mala-domacinska-korpica", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 10,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Domacinska,
            Velicina = VelicinaKorpice.Mala,
        };
        t.Db.Products.AddRange(obican, korpica);
        t.Db.SaveChanges();
        return (obican, korpica);
    }

    [Fact]
    public async Task Lista_proizvoda_ne_sadrzi_korpicu_ali_sadrzi_obicnu_robu()
    {
        using var t = new TestDb();
        Postavi(t);

        var rez = await new GetProductsHandler(t.Db).Handle(new GetProductsQuery(null, null, null), default);

        var p = Assert.Single(rez);
        Assert.Equal("babura-paprika", p.Slug);
        Assert.DoesNotContain(rez, x => x.Slug == "mala-domacinska-korpica");
    }

    [Fact]
    public async Task Proizvod_po_slugu_ne_resolvuje_korpicu()
    {
        using var t = new TestDb();
        Postavi(t);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductBySlugHandler(t.Db)
                .Handle(new GetProductBySlugQuery("mala-domacinska-korpica"), default));

        var obican = await new GetProductBySlugHandler(t.Db)
            .Handle(new GetProductBySlugQuery("babura-paprika"), default);
        Assert.Equal("Babura paprika", obican.Name);
    }
}
