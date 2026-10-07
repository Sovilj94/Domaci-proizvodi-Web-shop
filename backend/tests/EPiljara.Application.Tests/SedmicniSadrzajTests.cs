using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class SedmicniSadrzajTests
{
    private static Product Korpica(TestDb t)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);
        var k = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala,
        };
        t.Db.Products.Add(k);
        t.Db.SaveChanges();
        return k;
    }

    [Fact]
    public async Task Jedna_korpica_ne_moze_imati_dva_sadrzaja_za_istu_sedmicu()
    {
        using var t = new TestDb();
        var k = Korpica(t);

        t.Db.SedmicniSadrzaji.Add(new SedmicniSadrzaj { KorpicaId = k.Id, Godina = 2026, Sedmica = 41 });
        await t.Db.SaveChangesAsync();

        t.Db.SedmicniSadrzaji.Add(new SedmicniSadrzaj { KorpicaId = k.Id, Godina = 2026, Sedmica = 41 });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Brisanje_sadrzaja_brise_i_njegove_stavke()
    {
        using var t = new TestDb();
        var k = Korpica(t);
        var artikal = new Product
        {
            Name = "Paradajz", Slug = "paradajz", Price = 4m, Unit = Unit.Kg,
            CategoryId = k.CategoryId, ProducerId = k.ProducerId, MozeUKorpicu = true,
        };
        t.Db.Products.Add(artikal);

        var sadrzaj = new SedmicniSadrzaj { KorpicaId = k.Id, Godina = 2026, Sedmica = 41 };
        sadrzaj.Stavke.Add(new StavkaSadrzaja { ProductId = artikal.Id, Kolicina = 1.5m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(sadrzaj);
        await t.Db.SaveChangesAsync();

        t.Db.SedmicniSadrzaji.Remove(sadrzaj);
        await t.Db.SaveChangesAsync();

        Assert.Empty(await t.Db.StavkeSadrzaja.ToListAsync());
    }

    [Fact]
    public async Task Veza_korpica_kategorija_dozvoljava_istu_kategoriju_u_dva_tipa()
    {
        using var t = new TestDb();
        var kat = new Category { Name = "Mliječni proizvodi", Slug = "mlijecni-proizvodi" };
        t.Db.Categories.Add(kat);
        await t.Db.SaveChangesAsync();

        t.Db.KorpicaKategorije.AddRange(
            new KorpicaKategorija { TipKorpice = TipKorpice.Mlijecna, CategoryId = kat.Id },
            new KorpicaKategorija { TipKorpice = TipKorpice.Domacinska, CategoryId = kat.Id });
        await t.Db.SaveChangesAsync();

        Assert.Equal(2, await t.Db.KorpicaKategorije.CountAsync());
    }
}
