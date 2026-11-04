using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class GeneratorSadrzajaTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije =
        Options.Create(TestOpcije.Korpice());

    /// <summary>Korpica + kategorija + proizvođač + N artikala u bazenu.</summary>
    private static Product Postavi(TestDb t, int[] mjeseciArtikala, int brojArtikala = 10, decimal stock = 100m)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
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

        for (var i = 0; i < brojArtikala; i++)
            t.Db.Products.Add(new Product
            {
                Name = $"Artikal {i}", Slug = $"artikal-{i}", Price = 4m, Unit = Unit.Kg,
                CategoryId = kat.Id, ProducerId = proiz.Id, Stock = stock,
                MozeUKorpicu = true, Mjeseci = mjeseciArtikala,
            });

        t.Db.SaveChanges();
        return korpica;
    }

    /// <summary>
    /// Korpica + kategorija + proizvođač + N artikala po komadu, skupljih od budžeta po
    /// stavci (ali jeftinijih od ukupnog cilja) — suhomesnato/mliječno scenario.
    /// </summary>
    private static Product PostaviSkupeKomade(TestDb t, decimal cijenaArtikla, int brojArtikala = 10)
    {
        var kat = new Category { Name = "Suhomesnato", Slug = "suhomesnato" };
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        var korpica = new Product
        {
            Name = "Mala korpica suhomesnatog", Slug = "mala-korpica-suhomesnatog", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Suhomesnata,
            Velicina = VelicinaKorpice.Mala,
        };
        t.Db.Products.Add(korpica);
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Suhomesnata, CategoryId = kat.Id });

        for (var i = 0; i < brojArtikala; i++)
            t.Db.Products.Add(new Product
            {
                Name = $"Pečenica {i}", Slug = $"pecenica-{i}", Price = cijenaArtikla, Unit = Unit.Kom,
                CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 100m,
                MozeUKorpicu = true, Mjeseci = [],
            });

        t.Db.SaveChanges();
        return korpica;
    }

    private static GenerisiSedmicniSadrzajHandler Handler(TestDb t) => new(t.Db, Opcije);

    [Fact]
    public async Task Generise_nacrt_sa_najmanje_minimalnog_broja_artikala()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: []);   // prazno = cijele godine

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var r = Assert.Single(rez);
        Assert.Null(r.Upozorenje);
        Assert.True(r.BrojArtikala >= 5, $"očekivano >= 5, dobijeno {r.BrojArtikala}");

        var sadrzaj = await t.Db.SedmicniSadrzaji.Include(s => s.Stavke).SingleAsync();
        Assert.Equal(StatusSadrzaja.Nacrt, sadrzaj.Status);
        Assert.Equal(0m, sadrzaj.CiljnaVrijednost);   // zamrzava se tek pri objavi
    }

    /// <summary>
    /// Marža 45 %: nabavni budžet je 55 % cijene. Ranije je prvih nekoliko artikala pojelo cio budžet
    /// (budžet po stavci se računao po prodajnom cilju), pa je korpica ostajala ispod minimuma artikala.
    /// </summary>
    [Fact]
    public async Task Pri_visokoj_marzi_budzet_se_dijeli_tako_da_korpica_dobije_minimum_artikala()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: []);
        foreach (var p in t.Db.Products.Where(p => p.Vrsta != VrstaProizvoda.Korpica)) p.NabavnaCijena = 2.6m;
        await t.Db.SaveChangesAsync();
        var o = TestOpcije.Korpice();
        o.MinimalnaMarza = 0.45m;

        var r = Assert.Single(await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(o))
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.True(r.BrojArtikala >= 8, $"očekivano >= 8 artikala, dobijeno {r.BrojArtikala} ({r.Upozorenje})");
        Assert.True(r.Marza >= 0.45m, $"marža {r.Marza} ispod 45 %");
        Assert.True(r.NabavnaVrijednost <= korpica.Price * 0.55m);
    }

    /// <summary>Dodaje artikal u bazen korpice (ista kategorija kao Postavi).</summary>
    private static Product Dodaj(TestDb t, string naziv, Action<Product> izmjena)
    {
        var uzor = t.Db.Products.First(p => p.Vrsta != VrstaProizvoda.Korpica);
        var p = new Product
        {
            Name = naziv, Slug = naziv.ToLowerInvariant(), Price = 4m, Unit = Unit.Kg, CategoryId = uzor.CategoryId,
            ProducerId = uzor.ProducerId, Stock = 100m, MozeUKorpicu = true, Mjeseci = [],
        };
        izmjena(p);
        t.Db.Products.Add(p);
        t.Db.SaveChanges();
        return p;
    }

    /// <summary>Sedmica 41/2026 je u oktobru. Bundeva je glavna u oktobru — mora u kutiju, i to prva, bez obzira na nasumičnost.</summary>
    [Fact]
    public async Task Glavna_kultura_mjeseca_uvijek_ulazi_u_pocetni_sadrzaj()
    {
        for (var pokusaj = 0; pokusaj < 5; pokusaj++)
        {
            using var t = new TestDb();
            Postavi(t, mjeseciArtikala: [], brojArtikala: 14);
            var bundeva = Dodaj(t, "Bundeva", p => { p.Mjeseci = [9, 10, 11]; p.MjeseciGlavni = [10]; p.PrioritetUKorpici = 3; });
            var tikva = Dodaj(t, "Tikva", p => { p.Mjeseci = [9, 10, 11]; p.MjeseciGlavni = [9]; p.PrioritetUKorpici = 3; });

            var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

            var stavke = await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == r.SadrzajId).Select(s => s.ProductId).ToListAsync();
            Assert.Contains(bundeva.Id, stavke);
            // Tikva je glavna u septembru, ne u oktobru — u oktobru je obična dopuna (prioritet 3), ne forsira se.
            Assert.DoesNotContain(tikva.Id, stavke);
        }
    }

    [Fact]
    public async Task Opciona_kultura_nikad_nije_u_pocetnom_sadrzaju_ali_se_nudi_kao_zamjena()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], brojArtikala: 9);
        var kopriva = Dodaj(t, "Kopriva", p => { p.Opciono = true; p.PrioritetUKorpici = 0; p.NabavnaCijena = 1m; });

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        var s = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).Include(x => x.Zamjene).SingleAsync(x => x.Id == r.SadrzajId);
        Assert.DoesNotContain(s.Stavke, x => x.ProductId == kopriva.Id);
        Assert.Contains(s.Zamjene, x => x.ProductId == kopriva.Id);
    }

    [Fact]
    public async Task Kalendar_cuva_glavne_mjesece_samo_unutar_sezone_a_opciono_ih_brise()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], brojArtikala: 1);
        var bundeva = Dodaj(t, "Bundeva", p => p.Mjeseci = [9, 10, 11]);
        var h = new PostaviKalendarHandler(t.Db);

        var dto = await h.Handle(new PostaviKalendarCommand(bundeva.Id, true, [9, 10, 11], [10, 11, 12]), default);
        Assert.Equal([10, 11], dto.MjeseciGlavni);

        dto = await h.Handle(new PostaviKalendarCommand(bundeva.Id, true, [9, 10, 11], null, Opciono: true), default);
        Assert.True(dto.Opciono);
        Assert.Empty(dto.MjeseciGlavni);
    }

    /// <summary>
    /// Bazen je pun (10 artikala), ali zadata pakovanja su prevelika za nabavni budžet pri 45 % marže —
    /// upozorenje mora reći da je kriv budžet, ne „premalo artikala u bazenu".
    /// </summary>
    [Fact]
    public async Task Kad_budzet_ne_dozvoljava_minimum_upozorenje_kaze_da_je_kriv_budzet()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: []);
        foreach (var p in t.Db.Products.Where(p => p.Vrsta != VrstaProizvoda.Korpica))
        {
            p.NabavnaCijena = 2.4m;
            p.KolicinaMala = 1m; // 1 kg × 2,40 = 2,40 KM nabavno po artiklu; budžet 13,75 → 5 artikala
        }
        await t.Db.SaveChangesAsync();
        var o = TestOpcije.Korpice();
        o.MinimalnaMarza = 0.45m;

        var r = Assert.Single(await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(o))
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.Equal(5, r.BrojArtikala);
        Assert.Contains("Nabavni budžet", r.Upozorenje);
        Assert.Contains("5 od 8", r.Upozorenje);
        Assert.DoesNotContain("premalo artikala", r.Upozorenje);
    }

    [Fact]
    public async Task Preskace_artikle_van_sezone()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [1, 2]);   // sedmica 41 pada u oktobar

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var r = Assert.Single(rez);
        Assert.Equal(0, r.BrojArtikala);
        Assert.NotNull(r.Upozorenje);
    }

    [Fact]
    public async Task Ne_prelazi_raspolozive_zalihe()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], stock: 0.2m);

        await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var stavke = await t.Db.StavkeSadrzaja.ToListAsync();
        Assert.All(stavke, s => Assert.True(s.Kolicina <= 0.2m));
    }

    [Fact]
    public async Task Pogadja_cilj_generisanja_u_toleranciji()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: []);
        var cilj = korpica.Price * TestOpcije.Korpice().KoeficijentVrijednosti;

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var r = Assert.Single(rez);
        Assert.InRange(r.Vrijednost, cilj * 0.95m, cilj * 1.05m);
    }

    [Fact]
    public async Task Korpica_nikad_ne_ulazi_u_sadrzaj_druge_korpice()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: []);

        await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var idjevi = await t.Db.StavkeSadrzaja.Select(s => s.ProductId).ToListAsync();
        Assert.DoesNotContain(korpica.Id, idjevi);
    }

    [Fact]
    public async Task Ne_generise_dva_puta_za_istu_sedmicu()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: []);

        await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);
        var drugi = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        Assert.Empty(drugi);
        Assert.Equal(1, await t.Db.SedmicniSadrzaji.CountAsync());
    }

    [Fact]
    public async Task Uzima_minimalnu_kolicinu_kad_artikal_po_komadu_premasuje_budzet_po_stavci()
    {
        // Korpica od 25 KM → cilj 28.75, minimum 5 → budžet po stavci 5.75. Pečenica od
        // 10 KM/kom ne staje u 5.75 (Floor(5.75/10) = 0) ali staje u ukupan cilj od 28.75 —
        // generator ne smije vratiti praznu korpicu samo zato što je artikal skup po komadu.
        using var t = new TestDb();
        PostaviSkupeKomade(t, cijenaArtikla: 10m);

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var r = Assert.Single(rez);
        Assert.True(r.BrojArtikala >= 1, $"očekivano >= 1, dobijeno {r.BrojArtikala}");
    }

    [Fact]
    public async Task Prazan_bazen_vraca_nacrt_sa_upozorenjem_a_ne_gresku()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], brojArtikala: 0);

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var r = Assert.Single(rez);
        Assert.Equal(0, r.BrojArtikala);
        Assert.Contains("premalo", r.Upozorenje!, StringComparison.OrdinalIgnoreCase);
    }
}
