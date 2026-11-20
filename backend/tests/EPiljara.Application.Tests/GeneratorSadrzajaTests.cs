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
                Name = $"Povrće {i}", Slug = $"artikal-{i}", Price = 4m, Unit = Unit.Kg,
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

    /// <summary>
    /// Sedmica 41/2026 je u oktobru. Bundeva je glavna u oktobru — među kulturama istog prioriteta ide
    /// prva, bez obzira na nasumičnost.
    /// </summary>
    [Fact]
    public async Task Glavna_kultura_mjeseca_ide_prva_medju_kulturama_istog_prioriteta()
    {
        for (var pokusaj = 0; pokusaj < 5; pokusaj++)
        {
            using var t = new TestDb();
            Postavi(t, mjeseciArtikala: [], brojArtikala: 14);
            foreach (var p in t.Db.Products.Where(p => p.Vrsta == VrstaProizvoda.Obican)) p.PrioritetUKorpici = 3;
            t.Db.SaveChanges();
            var bundeva = Dodaj(t, "Bundeva", p => { p.Mjeseci = [9, 10, 11]; p.MjeseciGlavni = [10]; p.PrioritetUKorpici = 3; });
            var tikva = Dodaj(t, "Tikva", p => { p.Mjeseci = [9, 10, 11]; p.MjeseciGlavni = [9]; p.PrioritetUKorpici = 4; });

            var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

            var stavke = await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == r.SadrzajId).Select(s => s.ProductId).ToListAsync();
            Assert.Contains(bundeva.Id, stavke);
            // Tikva je glavna u septembru, ne u oktobru — u oktobru je obična dopuna, ne forsira se.
            Assert.DoesNotContain(tikva.Id, stavke);
        }
    }

    /// <summary>
    /// Osnova trpeze (prioritet 0: krompir, luk, mrkva) ide prije glavne kulture mjeseca — inače je cvekla
    /// istiskivala krompir iz male korpice.
    /// </summary>
    [Fact]
    public async Task Osnovna_namirnica_ide_prije_glavne_kulture_mjeseca()
    {
        for (var pokusaj = 0; pokusaj < 5; pokusaj++)
        {
            using var t = new TestDb();
            Postavi(t, mjeseciArtikala: [], brojArtikala: 10); // prioritet 0
            var cvekla = Dodaj(t, "Cvekla", p => { p.Mjeseci = [10, 11]; p.MjeseciGlavni = [10]; p.PrioritetUKorpici = 1; });

            var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

            var stavke = await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == r.SadrzajId).Select(s => s.ProductId).ToListAsync();
            Assert.Equal(8, stavke.Count);
            Assert.DoesNotContain(cvekla.Id, stavke);
        }
    }

    /* ---------------- težina korpice ---------------- */

    private static async Task<List<(Product Artikal, decimal Kolicina)>> StavkeSa(TestDb t, Guid sadrzajId) =>
        (await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == sadrzajId).Include(s => s.Product).ToListAsync())
        .Select(s => (s.Product!, s.Kolicina)).ToList();

    private static decimal Tezina(IEnumerable<(Product Artikal, decimal Kolicina)> stavke) =>
        stavke.Sum(s => s.Kolicina * (s.Artikal.KgPoJedinici ?? 0m));

    [Fact]
    public async Task Sadrzaj_ne_prelazi_najvecu_tezinu_korpice_i_ipak_ima_minimum_kultura()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 12); // 4 KM/kg → standardno 0,8 kg × 8 = 6,4 kg
        korpica.TezinaOd = 3m;
        korpica.TezinaDo = 5m;
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        var stavke = await StavkeSa(t, r.SadrzajId);
        Assert.Equal(8, stavke.Count);
        Assert.InRange(Tezina(stavke), 3m, 5m);
    }

    [Fact]
    public async Task Sezonska_kultura_ne_zauzima_vise_od_cetvrtine_najvece_tezine()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 7);
        korpica.TezinaOd = 3m;
        korpica.TezinaDo = 4m;
        // Zadate 2 kg cvekle u maloj korpici — upravo slučaj koji je vlasnik prijavio.
        var cvekla = Dodaj(t, "Cvekla", p => { p.Price = 2m; p.KolicinaMala = 2m; p.PrioritetUKorpici = 1; });
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        var stavka = Assert.Single(await StavkeSa(t, r.SadrzajId), s => s.Artikal.Id == cvekla.Id);
        Assert.True(stavka.Kolicina <= 1m, $"cvekla {stavka.Kolicina} kg"); // ¼ od 4 kg
    }

    [Fact]
    public async Task Komad_tezi_od_udjela_kulture_ne_ulazi_a_lakši_ulazi_po_tezini_komada()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 7);
        korpica.TezinaDo = 5m;
        var bundeva = Dodaj(t, "Bundeva", p => { p.Unit = Unit.Kom; p.TezinaKomada = 3m; p.PrioritetUKorpici = 1; });
        var karfiol = Dodaj(t, "Karfiol", p => { p.Unit = Unit.Kom; p.TezinaKomada = 1m; p.PrioritetUKorpici = 1; });
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        var stavke = await StavkeSa(t, r.SadrzajId);
        Assert.DoesNotContain(stavke, s => s.Artikal.Id == bundeva.Id); // 3 kg > ¼ od 5 kg
        var k = Assert.Single(stavke, s => s.Artikal.Id == karfiol.Id);
        Assert.Equal(1m, k.Kolicina);
        Assert.True(Tezina(stavke) <= 5m, $"{Tezina(stavke)} kg");
    }

    [Fact]
    public async Task Laksa_korpica_od_najmanje_tezine_se_dopunjava_osnovnom_namirnicom()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 8);
        korpica.TezinaOd = 8m;
        korpica.TezinaDo = 10m;
        // Jeftino po kilogramu, ali zadato samo 0,5 kg po kulturi → 8 kultura = 4 kg.
        foreach (var p in t.Db.Products.Where(p => p.Vrsta == VrstaProizvoda.Obican))
        {
            p.Price = 1m;
            p.KolicinaMala = 0.5m;
        }
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        var stavke = await StavkeSa(t, r.SadrzajId);
        Assert.Equal(8, stavke.Count);
        Assert.InRange(Tezina(stavke), 8m, 10m);
        Assert.All(stavke, s => Assert.True(s.Kolicina <= 5m)); // osnovna namirnica do pola od 10 kg
        Assert.Null(r.Upozorenje);
    }

    [Fact]
    public async Task Puna_tezina_sa_vrijednoscu_daleko_ispod_cilja_se_javlja()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 12);
        korpica.TezinaDo = 5m;
        // 1 KM/kg: 5 kg vrijedi 5 KM, a korpica od 25 KM cilja 28,75 KM.
        foreach (var p in t.Db.Products.Where(p => p.Vrsta == VrstaProizvoda.Obican)) p.Price = 1m;
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.Contains("Najveća težina (5 kg) je popunjena", r.Upozorenje);
    }

    [Fact]
    public async Task Komad_bez_unesene_tezine_se_javlja_u_upozorenju()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: [], brojArtikala: 1);
        korpica.TezinaDo = 5m;
        for (var i = 0; i < 8; i++)
            Dodaj(t, $"Komad{i}", p => { p.Unit = Unit.Kom; p.Price = 1.5m; });
        t.Db.SaveChanges();

        var r = Assert.Single(await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.Contains("Težina komada nije unesena", r.Upozorenje);
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
            p.NabavnaCijena = 3.2m;
            // Zadato 2 kg, najmanje pola = 1 kg × 3,20 = 3,20 KM nabavno po artiklu; budžet 13,75 → 4 artikla.
            p.KolicinaMala = 2m;
        }
        await t.Db.SaveChangesAsync();
        var o = TestOpcije.Korpice();
        o.MinimalnaMarza = 0.45m;

        var r = Assert.Single(await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(o))
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.Equal(4, r.BrojArtikala);
        Assert.Contains("Nabavni budžet", r.Upozorenje);
        Assert.Contains("4 od 8", r.Upozorenje);
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

    /// <summary>
    /// Prijavljena greška (18.11.2026): zadata količina je išla „cijela ili nikako", pa je nabavni
    /// budžet stao poslije šest kultura. Korpica 25 KM, marža 35 % → nabavno najviše 16,25 KM;
    /// osam cijelih zadatih količina bi koštalo 19,20 KM. Generator sad uzme manje od nekih, ali osam kultura.
    /// </summary>
    [Fact]
    public async Task Daje_minimum_kultura_i_kad_cijele_zadate_kolicine_ne_staju_u_budzet()
    {
        using var t = new TestDb();
        var korpica = Postavi(t, mjeseciArtikala: []);
        foreach (var p in t.Db.Products.Where(p => p.Vrsta != VrstaProizvoda.Korpica))
        {
            p.NabavnaCijena = 2.4m;
            p.KolicinaMala = 1m;
        }
        await t.Db.SaveChangesAsync();
        var o = TestOpcije.Korpice();
        o.MinimalnaMarza = 0.35m;

        var r = Assert.Single(await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(o))
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default));

        Assert.Equal(8, r.BrojArtikala);
        Assert.True(r.NabavnaVrijednost <= korpica.Price * 0.65m + 0.01m, $"nabavno {r.NabavnaVrijednost}");
        Assert.True(r.Vrijednost <= korpica.Price * o.KoeficijentVrijednosti + 0.01m);
        var stavke = await t.Db.StavkeSadrzaja.ToListAsync();
        Assert.All(stavke, s => Assert.InRange(s.Kolicina, 0.5m, 1m));   // između pola zadate i zadate
    }

    [Fact]
    public async Task Osnovne_namirnice_po_prioritetu_idu_prije_dopune()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], brojArtikala: 12);
        foreach (var p in t.Db.Products.Where(p => p.Vrsta != VrstaProizvoda.Korpica)) p.PrioritetUKorpici = 3;
        await t.Db.SaveChangesAsync();
        var krompir = Dodaj(t, "Krompir", p => { p.PrioritetUKorpici = 0; p.Price = 1.2m; });
        var luk = Dodaj(t, "Crni luk", p => { p.PrioritetUKorpici = 0; p.Price = 1.5m; });

        for (var i = 0; i < 5; i++)
        {
            var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null, Ponovo: true), default);
            var idjevi = await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == rez[0].SadrzajId)
                .Select(s => s.ProductId).ToListAsync();
            Assert.Contains(krompir.Id, idjevi);
            Assert.Contains(luk.Id, idjevi);
        }
    }

    [Fact]
    public async Task Ne_stavlja_dvije_vrste_iste_namirnice_dok_ima_drugih()
    {
        using var t = new TestDb();
        Postavi(t, mjeseciArtikala: [], brojArtikala: 1);
        string[] razne = ["Mrkva", "Cvekla", "Praziluk", "Karfiol", "Brokula", "Zelena salata", "Paprika roga", "Kupus"];
        foreach (var n in razne) Dodaj(t, n, _ => { });
        Dodaj(t, "Crveni krompir", _ => { });
        Dodaj(t, "Domaći krompir", _ => { });
        Dodaj(t, "Mladi krompirići", _ => { });
        Dodaj(t, "Crvena salata", _ => { });
        Dodaj(t, "Ljuta paprika", _ => { });

        var rez = await Handler(t).Handle(new GenerisiSedmicniSadrzajCommand(2026, 41, null), default);

        var nazivi = await t.Db.StavkeSadrzaja.Where(s => s.SedmicniSadrzajId == rez[0].SadrzajId)
            .Select(s => s.Product!.Name).ToListAsync();
        Assert.True(nazivi.Count >= 8);
        Assert.True(nazivi.Count(n => n.Contains("krompir", StringComparison.OrdinalIgnoreCase)) <= 1, string.Join(", ", nazivi));
        Assert.True(nazivi.Count(n => n.Contains("salata")) <= 1, string.Join(", ", nazivi));
        Assert.True(nazivi.Count(n => n.Contains("aprika")) <= 1, string.Join(", ", nazivi));
    }
}
