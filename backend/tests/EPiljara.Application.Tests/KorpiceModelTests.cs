using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KorpiceModelTests
{
    private static (Category, Producer) Sifarnik(TestDb t)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);
        t.Db.SaveChanges();
        return (kat, proiz);
    }

    [Fact]
    public async Task Mjeseci_prezivljavaju_upis_i_citanje()
    {
        using var t = new TestDb();
        var (kat, proiz) = Sifarnik(t);

        t.Db.Products.Add(new Product
        {
            Name = "Paradajz", Slug = "paradajz", Price = 4m, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id,
            MozeUKorpicu = true, Mjeseci = [6, 7, 8, 9],
        });
        await t.Db.SaveChangesAsync();

        var ucitan = await t.Db.Products.AsNoTracking().SingleAsync(p => p.Slug == "paradajz");
        Assert.Equal([6, 7, 8, 9], ucitan.Mjeseci);
    }

    [Fact]
    public async Task Prazni_mjeseci_se_citaju_kao_prazan_niz_a_ne_null()
    {
        using var t = new TestDb();
        var (kat, proiz) = Sifarnik(t);

        t.Db.Products.Add(new Product
        {
            Name = "Kajmak", Slug = "kajmak", Price = 12m, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id, MozeUKorpicu = true,
        });
        await t.Db.SaveChangesAsync();

        var ucitan = await t.Db.Products.AsNoTracking().SingleAsync(p => p.Slug == "kajmak");
        Assert.Empty(ucitan.Mjeseci);
    }

    [Fact]
    public async Task Korpica_cuva_tip_i_velicinu()
    {
        using var t = new TestDb();
        var (kat, proiz) = Sifarnik(t);

        t.Db.Products.Add(new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id,
            Vrsta = VrstaProizvoda.Korpica,
            TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala,
        });
        await t.Db.SaveChangesAsync();

        var ucitan = await t.Db.Products.AsNoTracking().SingleAsync(p => p.Slug == "mala-korpica-povrca");
        Assert.Equal(VrstaProizvoda.Korpica, ucitan.Vrsta);
        Assert.Equal(TipKorpice.Povrce, ucitan.TipKorpice);
        Assert.Equal(VelicinaKorpice.Mala, ucitan.Velicina);
    }
}
