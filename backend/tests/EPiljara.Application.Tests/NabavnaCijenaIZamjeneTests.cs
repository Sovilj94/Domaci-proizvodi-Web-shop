using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Nabavna cijena na proizvodu, generator koji poštuje minimalnu maržu, predložene zamjene i
/// kupčeva zamjena jednog artikla drugim (količinu određuje server).
/// </summary>
public class NabavnaCijenaIZamjeneTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije = Options.Create(new KorpiceOpcije());

    /// <summary>Korpica 25 KM (cilj 28.75, nabavni budžet 18.75) + artikli po 4 KM/kg.</summary>
    private static Product Postavi(TestDb t, params decimal?[] nabavne)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Milan Petrović", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        var korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala,
        };
        t.Db.Products.Add(korpica);
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = kat.Id });

        for (var i = 0; i < nabavne.Length; i++)
            t.Db.Products.Add(new Product
            {
                Name = $"Artikal {i}", Slug = $"artikal-{i}", Price = 4m, Unit = Unit.Kg,
                CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 100m,
                MozeUKorpicu = true, NabavnaCijena = nabavne[i],
            });

        t.Db.SaveChanges();
        return korpica;
    }

    private static Task<IReadOnlyList<RezultatGenerisanjaDto>> Generisi(TestDb t, bool ponovo = false) =>
        new GenerisiSedmicniSadrzajHandler(t.Db, Opcije)
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null, ponovo), default);

    /* ---------- generator ---------- */

    [Fact]
    public async Task Generator_ne_prelazi_nabavni_budzet_i_javlja_kad_ga_on_ogranici()
    {
        using var t = new TestDb();
        Postavi(t, Enumerable.Repeat<decimal?>(3.5m, 10).ToArray());   // marža artikla samo 12.5%

        var r = Assert.Single(await Generisi(t));

        Assert.True(r.NabavnaVrijednost <= 18.75m, $"nabavno {r.NabavnaVrijednost} > 18.75");
        Assert.True(r.Marza >= 0.25m, $"marža {r.Marza}");
        Assert.Contains("Nabavni budžet", r.Upozorenje);
        Assert.Empty(r.BezNabavneCijene);
    }

    [Fact]
    public async Task Bez_nabavnih_cijena_generator_radi_kao_prije_i_navodi_artikle()
    {
        using var t = new TestDb();
        Postavi(t, new decimal?[10]);

        var r = Assert.Single(await Generisi(t));

        Assert.Null(r.Upozorenje);
        Assert.Null(r.Marza);
        Assert.Equal(r.BrojArtikala, r.BezNabavneCijene.Count);
        Assert.True(r.Vrijednost >= r.CiljGenerisanja * 0.95m);
    }

    [Fact]
    public async Task Zamjene_su_van_kutije_i_bez_artikala_koji_bi_oborili_marzu()
    {
        using var t = new TestDb();
        // 6 artikala sa dobrom maržom (50%), 6 sa lošom (87.5% nabavna/prodajna).
        Postavi(t, [2m, 2m, 2m, 2m, 2m, 2m, 3.5m, 3.5m, 3.5m, 3.5m, 3.5m, 3.5m]);

        var r = Assert.Single(await Generisi(t));

        var sadrzaj = await t.Db.SedmicniSadrzaji
            .Include(s => s.Stavke).Include(s => s.Zamjene).ThenInclude(z => z.Product)
            .SingleAsync();
        Assert.Equal(sadrzaj.Zamjene.Count, r.BrojZamjena);
        Assert.InRange(sadrzaj.Zamjene.Count, 1, Opcije.Value.BrojZamjena);
        Assert.All(sadrzaj.Zamjene, z =>
        {
            Assert.DoesNotContain(sadrzaj.Stavke, s => s.ProductId == z.ProductId);
            Assert.Equal(2m, z.Product!.NabavnaCijena);
        });
    }

    [Fact]
    public async Task Ponovno_generisanje_mijenja_nacrt_ali_ne_dira_objavljen_sadrzaj()
    {
        using var t = new TestDb();
        Postavi(t, new decimal?[10]);
        await Generisi(t);

        Assert.Empty(await Generisi(t));                       // bez Ponovo se preskače
        var drugi = Assert.Single(await Generisi(t, ponovo: true));
        Assert.Equal(drugi.SadrzajId, (await t.Db.SedmicniSadrzaji.SingleAsync()).Id);

        var s = await t.Db.SedmicniSadrzaji.SingleAsync();
        s.Status = StatusSadrzaja.Objavljen;
        await t.Db.SaveChangesAsync();

        Assert.Empty(await Generisi(t, ponovo: true));
        Assert.Equal(s.Id, (await t.Db.SedmicniSadrzaji.SingleAsync()).Id);
    }

    /* ---------- admin: nacrt, objava, proizvod ---------- */

    [Fact]
    public async Task Nacrt_cuva_zamjene_a_artikal_koji_udje_u_kutiju_ispada_iz_zamjena()
    {
        using var t = new TestDb();
        Postavi(t, [2m, 2m, 2m]);
        var artikli = await t.Db.Products.Where(p => p.Vrsta == VrstaProizvoda.Obican)
            .OrderBy(p => p.Name).ToListAsync();
        var sadrzaj = new SedmicniSadrzaj
        {
            KorpicaId = (await t.Db.Products.SingleAsync(p => p.Vrsta == VrstaProizvoda.Korpica)).Id,
            Godina = 2026, Sedmica = 41,
        };
        t.Db.SedmicniSadrzaji.Add(sadrzaj);
        await t.Db.SaveChangesAsync();
        var handler = new IzmijeniNacrtHandler(t.Db, Opcije);

        var dto = await handler.Handle(new IzmijeniNacrtCommand(
            sadrzaj.Id, [new StavkaUnos(artikli[0].Id, 2m)], [artikli[1].Id, artikli[2].Id]), default);
        Assert.Equal(2, dto.Zamjene.Count);
        Assert.Equal(4m, dto.NabavnaVrijednost);              // 2 kg × 2 KM
        Assert.Equal(0.84m, dto.Marza);                        // (25 − 4) / 25

        t.Db.ChangeTracker.Clear();
        dto = await handler.Handle(new IzmijeniNacrtCommand(
            sadrzaj.Id, [new StavkaUnos(artikli[0].Id, 2m), new StavkaUnos(artikli[1].Id, 1m)], null), default);
        Assert.Equal(artikli[2].Id, Assert.Single(dto.Zamjene).ProductId);
    }

    [Fact]
    public async Task Objava_izbacuje_nedostupnu_zamjenu()
    {
        using var t = new TestDb();
        Postavi(t, [2m, 2m]);
        var artikli = await t.Db.Products.Where(p => p.Vrsta == VrstaProizvoda.Obican)
            .OrderBy(p => p.Name).ToListAsync();
        var sadrzaj = new SedmicniSadrzaj
        {
            KorpicaId = (await t.Db.Products.SingleAsync(p => p.Vrsta == VrstaProizvoda.Korpica)).Id,
            Godina = 2026, Sedmica = 41,
        };
        sadrzaj.Stavke.Add(new StavkaSadrzaja { ProductId = artikli[0].Id, Kolicina = 1m, JedinicnaCijena = 4m });
        sadrzaj.Zamjene.Add(new ZamjenaSadrzaja { ProductId = artikli[1].Id, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(sadrzaj);
        artikli[1].IsAvailable = false;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var dto = await new ObjaviSadrzajHandler(t.Db, Opcije)
            .Handle(new ObjaviSadrzajCommand(sadrzaj.Id), default);

        Assert.Empty(dto.Zamjene);
        Assert.Equal(4m, dto.CiljnaVrijednost);
    }

    [Fact]
    public async Task Proizvod_cuva_nabavnu_cijenu_a_javni_dto_je_nema()
    {
        using var t = new TestDb();
        var korpica = Postavi(t);
        var input = new ProductInput("Paradajz", 5m, "kg", 10m, korpica.CategoryId, korpica.ProducerId,
            "", null, true, false, NabavnaCijena: 3m);

        var dto = await new CreateProductHandler(t.Db).Handle(new CreateProductCommand(input), default);
        Assert.Equal(3m, dto.NabavnaCijena);
        Assert.Equal(0.4m, dto.Marza);

        // Izostavljena nabavna cijena na izmjeni = ne mijenjaj; brisanje se traži izričito.
        var bez = await new UpdateProductHandler(t.Db)
            .Handle(new UpdateProductCommand(dto.Id, input with { NabavnaCijena = null }), default);
        Assert.Equal(3m, bez.NabavnaCijena);
        var obrisana = await new UpdateProductHandler(t.Db)
            .Handle(new UpdateProductCommand(dto.Id, input with { UkloniNabavnuCijenu = true }), default);
        Assert.Null(obrisana.NabavnaCijena);

        Assert.Null(typeof(ProductDto).GetProperty("NabavnaCijena"));
        Assert.Null(typeof(SadrzajDto).GetProperty("NabavnaVrijednost"));
    }

    /* ---------- kupčeva zamjena ---------- */

    /// <summary>Scenario (cilj 7.00 = Krompir 2kg×2 + Paprika 1kg×3) + Tikvica 1 KM/kg, cijele godine.</summary>
    private static async Task<(Scenario s, Guid orderItemId, Product tikvica)> ScenarioSaTikvicom(
        decimal? nabavnaTikvice = null)
    {
        var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var krompir = await s.Db.Products.SingleAsync(p => p.Id == s.KrompirId);
        var tikvica = new Product
        {
            Name = "Tikvica", Slug = "tikvica", Price = 1m, Unit = Unit.Kg, Stock = 100m,
            MozeUKorpicu = true, CategoryId = krompir.CategoryId, ProducerId = krompir.ProducerId,
            NabavnaCijena = nabavnaTikvice,
        };
        s.Db.Products.Add(tikvica);
        await s.Db.SaveChangesAsync();

        var narudzba = await s.PosaljiKorpicuAsync(1m);
        var id = await s.OrderItemIdAsync(narudzba.Id);
        s.Db.ChangeTracker.Clear();
        return (s, id, tikvica);
    }

    [Fact]
    public async Task Zamjena_odredjuje_kolicinu_po_vrijednosti_artikla_koji_izlazi()
    {
        var (s, id, tikvica) = await ScenarioSaTikvicom();
        using var _ = s;

        var r = await s.ZamijeniAsync(id, s.KrompirId, tikvica.Id);

        var nova = Assert.Single(r.Stavke, x => x.ProductId == tikvica.Id);
        Assert.Equal(4m, nova.Kolicina);                  // 2 kg krompira × 2 KM = 4 kg tikvice × 1 KM
        Assert.True(nova.DodaoKupac);
        Assert.DoesNotContain(r.Stavke, x => x.ProductId == s.KrompirId);
        Assert.Equal(7m, r.Korpica.TrenutnaVrijednost);
    }

    [Fact]
    public async Task Zamjena_koja_ne_moze_stati_u_budzet_se_odbija()
    {
        var (s, id, _) = await ScenarioSaTikvicom();
        using var __ = s;

        // Krompir (4.00) za Sir 3 KM/kom: 1 kom = 6.00 (< 6.65), 2 kom = 9.00 (> 7.00).
        var ex = await Assert.ThrowsAsync<DomainException>(() => s.ZamijeniAsync(id, s.KrompirId, s.SirId));
        Assert.Equal("zamjena_ne_staje", ex.Code);
    }

    [Fact]
    public async Task Zamjena_van_sezone_i_dupla_se_odbijaju()
    {
        var (s, id, _) = await ScenarioSaTikvicom();
        using var __ = s;

        var vanSezone = await Assert.ThrowsAsync<DomainException>(
            () => s.ZamijeniAsync(id, s.KrompirId, s.VanSezoneId));
        Assert.Equal("artikal_nije_podoban", vanSezone.Code);

        var dupla = await Assert.ThrowsAsync<DomainException>(
            () => s.ZamijeniAsync(id, s.KrompirId, s.PaprikaId));
        Assert.Equal("artikal_vec_u_korpici", dupla.Code);
    }

    [Fact]
    public async Task Zamjena_koja_obara_marzu_se_odbija()
    {
        // Korpica 10 KM, marža 25% → nabavni budžet 7.50. Paprika 1 kg nabavno 2.00, a 4 kg
        // tikvice po 1.50 = 6.00 → ukupno 8.00 > 7.50 i više nego prije (krompir 2 kg × 1.00).
        var (s, id, tikvica) = await ScenarioSaTikvicom(nabavnaTikvice: 1.5m);
        using var _ = s;
        await s.Db.Products.Where(p => p.Id == s.KrompirId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.NabavnaCijena, 1m));
        await s.Db.Products.Where(p => p.Id == s.PaprikaId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.NabavnaCijena, 2m));

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.ZamijeniAsync(id, s.KrompirId, tikvica.Id));
        Assert.Equal("zamjena_nije_dozvoljena", ex.Code);
        Assert.DoesNotContain("marž", ex.Message);
    }

    [Fact]
    public async Task Preporucene_zamjene_su_prve_u_ponudi()
    {
        var (s, id, tikvica) = await ScenarioSaTikvicom();
        using var _ = s;
        var sadrzaj = await s.Db.SedmicniSadrzaji.SingleAsync();
        s.Db.ZamjeneSadrzaja.Add(new ZamjenaSadrzaja
        {
            SedmicniSadrzajId = sadrzaj.Id, ProductId = tikvica.Id, JedinicnaCijena = 1m,
        });
        await s.Db.SaveChangesAsync();
        s.Db.ChangeTracker.Clear();

        var d = await s.MojaKorpicaAsync(id);

        Assert.Equal(tikvica.Id, d.Ponuda[0].ProductId);
        Assert.True(d.Ponuda[0].PreporucenaZamjena);
        Assert.Single(d.Ponuda, p => p.PreporucenaZamjena);
    }

    [Theory]
    [InlineData(4.0, 3.0, 1.0, "kg", 4.0)]     // tačno po vrijednosti
    [InlineData(3.0, 4.0, 2.64, "kom", 1.0)]   // 1 kom = 6.64 je ispod 6.65 → nema
    [InlineData(3.0, 4.0, 1.5, "kg", 2.0)]     // 2 kg = 7.00
    public void Kolicina_zamjene(double stara, double ostalo, double cijena, string jedinica, double? ocekivano)
    {
        var unit = jedinica == "kom" ? Unit.Kom : Unit.Kg;
        var k = ZamijeniArtikalUKorpiciHandler.Kolicina(
            (decimal)stara, (decimal)ostalo, (decimal)cijena, 6.65m, 7m, unit);

        if (jedinica == "kom") Assert.Null(k);
        else Assert.Equal((decimal?)ocekivano, k);
    }
}
