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

        var rez = await new GetProductsHandler(t.Db, TestOpcije.KorpiceO(), TestOpcije.ProdavnicaO()).Handle(new GetProductsQuery(null, null, null), default);

        var p = Assert.Single(rez);
        Assert.Equal("babura-paprika", p.Slug);
        Assert.DoesNotContain(rez, x => x.Slug == "mala-domacinska-korpica");
    }

    [Fact]
    public async Task Filter_po_proizvodjacu_vraca_samo_njegove_obicne_artikle()
    {
        using var t = new TestDb();
        var (obican, _) = Postavi(t);   // paprika + korpica, isti proizvođač
        var drugi = new Producer { Name = "Porodica Drugi", Village = "Ljubačevo" };
        t.Db.Producers.Add(drugi);
        t.Db.Products.Add(new Product
        {
            Name = "Tuđi sir", Slug = "tudji-sir", Price = 9m, Unit = Unit.Kg,
            CategoryId = obican.CategoryId, ProducerId = drugi.Id, Stock = 3,
            Vrsta = VrstaProizvoda.Obican,
        });
        t.Db.SaveChanges();

        var rez = await new GetProductsHandler(t.Db, TestOpcije.KorpiceO(), TestOpcije.ProdavnicaO())
            .Handle(new GetProductsQuery(null, null, null, obican.ProducerId), default);

        Assert.Equal(["babura-paprika"], rez.Select(p => p.Slug));
    }

    [Fact]
    public async Task Proizvod_po_slugu_ne_resolvuje_korpicu()
    {
        using var t = new TestDb();
        Postavi(t);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetProductBySlugHandler(t.Db, TestOpcije.KorpiceO(), TestOpcije.ProdavnicaO())
                .Handle(new GetProductBySlugQuery("mala-domacinska-korpica"), default));

        var obican = await new GetProductBySlugHandler(t.Db, TestOpcije.KorpiceO(), TestOpcije.ProdavnicaO())
            .Handle(new GetProductBySlugQuery("babura-paprika"), default);
        Assert.Equal("Babura paprika", obican.Name);
    }

    /// <summary>
    /// Kartica domaćina na početnoj kaže šta on proizvodi. Korpica nije njegov proizvod
    /// (seeder je za FK vezuje za prvog proizvođača), a nedostupan artikal kupac ne može
    /// kupiti — nijedno ne ide u listu. Najviše četiri, po imenu.
    /// </summary>
    [Fact]
    public async Task Proizvodjac_nosi_do_cetiri_dostupna_obicna_artikla_po_imenu()
    {
        using var t = new TestDb();
        var (obican, _) = Postavi(t);   // "Babura paprika" + korpica, isti proizvođač
        foreach (var (ime, dostupan) in new[]
                 { ("Sir", true), ("Kajmak", true), ("Med", true), ("Ajvar", true), ("Pekmez", false) })
        {
            t.Db.Products.Add(new Product
            {
                Name = ime, Slug = ime.ToLowerInvariant(), Price = 5m, Unit = Unit.Kom,
                CategoryId = obican.CategoryId, ProducerId = obican.ProducerId, Stock = 5,
                IsAvailable = dostupan, Vrsta = VrstaProizvoda.Obican,
            });
        }
        t.Db.SaveChanges();

        var rez = await new GetProducersHandler(t.Db).Handle(new GetProducersQuery(), default);

        var p = Assert.Single(rez);
        Assert.Equal(["Ajvar", "Babura paprika", "Kajmak", "Med"], p.Artikli);
    }

    [Fact]
    public async Task Proizvodjac_bez_artikala_ima_praznu_listu()
    {
        using var t = new TestDb();
        t.Db.Producers.Add(new Producer { Name = "Porodica Bez", Village = "Ljubačevo" });
        t.Db.SaveChanges();

        var rez = await new GetProducersHandler(t.Db).Handle(new GetProducersQuery(), default);

        Assert.Empty(Assert.Single(rez).Artikli);
    }
}
