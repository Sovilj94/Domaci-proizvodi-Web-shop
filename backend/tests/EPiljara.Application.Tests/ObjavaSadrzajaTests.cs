using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class ObjavaSadrzajaTests
{
    private static (SedmicniSadrzaj, Product) Postavi(TestDb t, decimal cijenaArtikla = 4m)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        var korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala,
        };
        var artikal = new Product
        {
            Name = "Paradajz", Slug = "paradajz", Price = cijenaArtikla, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50, MozeUKorpicu = true,
        };
        t.Db.Products.AddRange(korpica, artikal);

        var sadrzaj = new SedmicniSadrzaj { KorpicaId = korpica.Id, Godina = 2026, Sedmica = 41 };
        sadrzaj.Stavke.Add(new StavkaSadrzaja
        {
            ProductId = artikal.Id, Kolicina = 2m, JedinicnaCijena = cijenaArtikla,
        });
        t.Db.SedmicniSadrzaji.Add(sadrzaj);
        t.Db.SaveChanges();
        return (sadrzaj, artikal);
    }

    [Fact]
    public async Task Objava_zamrzava_ciljnu_vrijednost_kao_zbir_stavki()
    {
        using var t = new TestDb();
        Postavi(t, cijenaArtikla: 4m);
        var id = await t.Db.SedmicniSadrzaji.Select(s => s.Id).SingleAsync();

        await new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(id), default);

        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Objavljen, s.Status);
        Assert.Equal(8m, s.CiljnaVrijednost);          // 2 kg × 4
        Assert.NotNull(s.ObjavljenU);
    }

    [Fact]
    public async Task Objava_ponovo_snima_jedinicnu_cijenu_iz_zivog_artikla()
    {
        using var t = new TestDb();
        var (_, artikal) = Postavi(t, cijenaArtikla: 4m);
        var id = await t.Db.SedmicniSadrzaji.Select(s => s.Id).SingleAsync();

        artikal.Price = 5m;                             // admin promijenio cijenu prije objave
        await t.Db.SaveChangesAsync();

        await new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(id), default);

        var stavka = await t.Db.StavkeSadrzaja.AsNoTracking().SingleAsync();
        Assert.Equal(5m, stavka.JedinicnaCijena);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(10m, s.CiljnaVrijednost);          // 2 kg × 5
    }

    [Fact]
    public async Task Prazan_sadrzaj_se_ne_moze_objaviti()
    {
        using var t = new TestDb();
        var (sadrzaj, _) = Postavi(t);
        t.Db.StavkeSadrzaja.RemoveRange(t.Db.StavkeSadrzaja);
        await t.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(sadrzaj.Id), default));
        Assert.Equal("sadrzaj_prazan", ex.Code);
    }

    [Fact]
    public async Task Dvostruka_objava_baca_konflikt()
    {
        using var t = new TestDb();
        Postavi(t);
        var id = await t.Db.SedmicniSadrzaji.Select(s => s.Id).SingleAsync();

        await new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(id), default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(id), default));
    }

    [Fact]
    public async Task Nepostojeci_sadrzaj_baca_not_found()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ObjaviSadrzajHandler(t.Db).Handle(new ObjaviSadrzajCommand(Guid.NewGuid()), default));
    }
}
