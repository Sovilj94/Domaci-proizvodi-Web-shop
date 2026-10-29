using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class KorpiceQueriesTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije = Options.Create(new KorpiceOpcije());
    private static readonly IOptions<EPiljara.Application.Features.Pretplate.PretplateOpcije> PretplateOpc =
        Options.Create(new EPiljara.Application.Features.Pretplate.PretplateOpcije());

    private static SedmicniSadrzaj Postavi(TestDb t, StatusSadrzaja status, int godina, int sedmica)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        var korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m,
            Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 40,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce,
            Velicina = VelicinaKorpice.Mala,
        };
        var artikal = new Product
        {
            Name = "Paradajz", Slug = "paradajz", Price = 4m, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50, MozeUKorpicu = true,
        };
        t.Db.Products.AddRange(korpica, artikal);

        var s = new SedmicniSadrzaj
        {
            KorpicaId = korpica.Id, Godina = godina, Sedmica = sedmica,
            Status = status, CiljnaVrijednost = 8m,
        };
        s.Stavke.Add(new StavkaSadrzaja { ProductId = artikal.Id, Kolicina = 2m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(s);
        t.Db.SaveChanges();
        return s;
    }

    private static (int godina, int sedmica) Sada()
    {
        var d = DateTime.UtcNow;
        return (System.Globalization.ISOWeek.GetYear(d), System.Globalization.ISOWeek.GetWeekOfYear(d));
    }

    [Fact]
    public async Task Lista_vraca_samo_korpice_ne_i_obicnu_robu()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);

        var rez = await new GetKorpiceHandler(t.Db, PretplateOpc).Handle(new GetKorpiceQuery(null), default);

        var k = Assert.Single(rez);
        Assert.Equal("mala-korpica-povrca", k.Slug);
    }

    [Fact]
    public async Task Lista_se_filtrira_po_tipu()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);

        Assert.Single(await new GetKorpiceHandler(t.Db, PretplateOpc).Handle(new GetKorpiceQuery("povrce"), default));
        Assert.Empty(await new GetKorpiceHandler(t.Db, PretplateOpc).Handle(new GetKorpiceQuery("mlijecna"), default));
    }

    [Fact]
    public async Task Objavljen_sadrzaj_nosi_proizvodjaca_po_stavci()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);

        // Bez ovoga, EF Core-ov change tracker bi "besplatno" popunio Product i Producer
        // navigacije iz već praćenih entiteta nastalih u Postavi() (relationship fixup), pa
        // bi test prošao i bez ThenInclude lanca u handleru. Svježi DbContext po zahtjevu u
        // produkciji nema taj efekat — ovim ga simuliramo.
        t.Db.ChangeTracker.Clear();

        var dto = await new GetSadrzajKorpiceHandler(t.Db, Options.Create(new KorpiceOpcije()))
            .Handle(new GetSadrzajKorpiceQuery("mala-korpica-povrca"), default);

        var stavka = Assert.Single(dto.Stavke);
        Assert.Equal("Paradajz", stavka.Naziv);
        Assert.Equal("Marko Marković", stavka.ProizvodjacNaziv);
        Assert.Equal("Stričići", stavka.ProizvodjacSelo);
        Assert.Equal("kg", stavka.Jedinica);
    }

    [Fact]
    public async Task Javni_sadrzaj_nudi_dodatne_kulture_iz_sezone_i_kategorija_korpice()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        var sadrzaj = Postavi(t, StatusSadrzaja.Objavljen, g, s);
        var paradajz = await t.Db.Products.SingleAsync(p => p.Slug == "paradajz");
        var mlijecni = new Category { Name = "Mliječni", Slug = "mlijecni" };
        t.Db.Categories.Add(mlijecni);
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = paradajz.CategoryId });
        var mjesec = System.Globalization.ISOWeek.ToDateTime(g, s, DayOfWeek.Thursday).Month;
        Product Artikal(string naziv, Action<Product>? izmjena = null)
        {
            var p = new Product
            {
                Name = naziv, Slug = naziv.ToLowerInvariant(), Price = 2m, Unit = Unit.Kg, Stock = 10,
                MozeUKorpicu = true, CategoryId = paradajz.CategoryId, ProducerId = paradajz.ProducerId,
            };
            izmjena?.Invoke(p);
            t.Db.Products.Add(p);
            return p;
        }
        Artikal("Tikvica");
        Artikal("Grah", p => p.PrioritetUKorpici = 1);
        Artikal("Malina", p => p.Mjeseci = [mjesec % 12 + 1]);          // van sezone
        Artikal("Blitva", p => p.Stock = 0);                            // bez zalihe
        Artikal("Sir", p => p.CategoryId = mlijecni.Id);                // druga vrsta korpice
        var kajmak = Artikal("Kajmak", p => p.CategoryId = mlijecni.Id);
        t.Db.ZamjeneSadrzaja.Add(new ZamjenaSadrzaja { SedmicniSadrzajId = sadrzaj.Id, ProductId = kajmak.Id, JedinicnaCijena = 2m });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var dto = await new GetSadrzajKorpiceHandler(t.Db, Opcije).Handle(new GetSadrzajKorpiceQuery("mala-korpica-povrca"), default);

        // Svježe prije dopune; predložena zamjena smije i iz druge kategorije; paradajz je već u kutiji.
        Assert.Equal(["Kajmak", "Tikvica", "Grah"], dto.Dodatne.Select(d => d.Naziv).ToArray());
        Assert.All(dto.Dodatne, d => Assert.False(string.IsNullOrEmpty(d.Boja)));
        // Od koga kupujem: domaćin i selo kao na stavkama kutije.
        Assert.All(dto.Dodatne, d =>
        {
            Assert.Equal("Marko Marković", d.ProizvodjacNaziv);
            Assert.Equal("Stričići", d.ProizvodjacSelo);
        });
    }

    [Fact]
    public async Task Sezona_daje_kulture_po_mjesecima_sa_kolicinom_po_velicini()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);
        var paradajz = await t.Db.Products.SingleAsync(p => p.Slug == "paradajz");
        paradajz.Mjeseci = [7, 8];
        paradajz.Stock = 0;                                              // zaliha ne smeta pregledu godine
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = paradajz.CategoryId });
        t.Db.Products.Add(new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 1.5m, Unit = Unit.Kg, Stock = 10, MozeUKorpicu = true,
            CategoryId = paradajz.CategoryId, ProducerId = paradajz.ProducerId,
        });
        await t.Db.SaveChangesAsync();

        var dto = await new GetSezonaKorpiceHandler(t.Db, Opcije).Handle(new GetSezonaKorpiceQuery("povrce"), default);

        Assert.Equal(12, dto.Mjeseci.Count);
        Assert.Equal(["Krompir"], dto.Mjeseci[0].Kulture.Select(k => k.Naziv).ToArray());
        Assert.Equal(["Krompir", "Paradajz"], dto.Mjeseci[6].Kulture.Select(k => k.Naziv).ToArray());
        // Mala korpica 25 KM: cilj 28.75 na 8 artikala ≈ 3.59 KM po artiklu → paradajz 4 KM/kg ≈ 0.8 kg.
        Assert.Equal(0.8m, dto.Mjeseci[6].Kulture[1].Kolicine["mala"]);
        var mala = Assert.Single(dto.Velicine);
        Assert.Equal(("mala", 8, 28.75m), (mala.Velicina, mala.BrojNamirnica, mala.Vrijednost));
    }

    [Fact]
    public async Task Veca_korpica_pokazuje_vecu_kolicinu_iste_namirnice()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);
        var paradajz = await t.Db.Products.SingleAsync(p => p.Slug == "paradajz");
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = paradajz.CategoryId });
        t.Db.Products.Add(new Product
        {
            Name = "Velika korpica povrća", Slug = "velika-korpica-povrca", Price = 45m, Unit = Unit.Kom,
            CategoryId = paradajz.CategoryId, ProducerId = paradajz.ProducerId, Stock = 40,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Velika,
        });
        await t.Db.SaveChangesAsync();

        var dto = await new GetSezonaKorpiceHandler(t.Db, Opcije).Handle(new GetSezonaKorpiceQuery("povrce"), default);

        var kolicine = dto.Mjeseci[0].Kulture.Single(k => k.Naziv == "Paradajz").Kolicine;
        // Oba dijele na broj artikala najmanje korpice: velika (45 KM) nosi oko 1,8× više od male (25 KM).
        Assert.Equal(0.8m, kolicine["mala"]);
        Assert.True(kolicine["velika"] > kolicine["mala"] * 1.6m, $"velika {kolicine["velika"]} nije veća od male");
    }

    [Fact]
    public async Task Nacrt_nije_vidljiv_javnom_upitu()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Nacrt, g, s);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSadrzajKorpiceHandler(t.Db, Options.Create(new KorpiceOpcije()))
                .Handle(new GetSadrzajKorpiceQuery("mala-korpica-povrca"), default));
        Assert.Contains("sadrzaj_nije_objavljen", ex.Message);
    }

    /// <summary>
    /// Regresioni test za bag: rok se računao od PONOĆI dana isporuke (ISOWeek.ToDateTime
    /// vraća ponoć), ignorišući DeliverySlot.TimeFrom. Sa TimeFrom="16:00" i CutoffHours=48,
    /// ponoćni računa rok "srijeda 00:00" umjesto tačnog "srijeda 16:00" — razlika od 16h,
    /// baš dovoljna da ispravna i pogrešna implementacija dadnu različite odgovore. Prije
    /// popravke ovaj test CRVENO puca na obje asertacije (vrijeme i Kind).
    /// </summary>
    [Fact]
    public async Task Rok_i_isporuka_racunaju_se_sa_vremenom_slota_ne_sa_ponocu()
    {
        using var t = new TestDb();
        t.Db.DeliverySlots.Add(new DeliverySlot
        {
            DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48,
        });
        t.Db.SaveChanges();

        var dto = await new GetTekucaSedmicaHandler(t.Db, Opcije)
            .Handle(new GetTekucaSedmicaQuery(), default);

        var (godina, sedmica) = Sada();
        // A4: „16:00" je lokalno vrijeme (Sarajevo), na žici UTC.
        var ocekivanaIsporuka = TimeZoneInfo.ConvertTimeToUtc(
            System.Globalization.ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).AddHours(16), SedmicaHelper.Zona);
        var ocekivaniRok = ocekivanaIsporuka.AddHours(-48);

        Assert.Equal(ocekivanaIsporuka, dto.DatumIsporuke);
        Assert.Equal(ocekivaniRok, dto.RokNarucivanja);
        Assert.Equal(DateTimeKind.Utc, dto.DatumIsporuke.Kind);
    }

    /// <summary>
    /// TimeFrom je slobodan unos na DeliverySlot-u, bez validacije na modelu. Ako je
    /// nekim putem ispao neparsabilan, rok ne smije da sruši zahtjev — pada na ponoć
    /// (00:00), isto ponašanje kao kad bi TimeFrom bio "00:00".
    /// </summary>
    [Fact]
    public async Task Neparsabilan_TimeFrom_pada_na_ponoc_umjesto_da_puca()
    {
        using var t = new TestDb();
        t.Db.DeliverySlots.Add(new DeliverySlot
        {
            DayOfWeek = 4, TimeFrom = "nije-vrijeme", TimeTo = "20:00", CutoffHours = 48,
        });
        t.Db.SaveChanges();

        var dto = await new GetTekucaSedmicaHandler(t.Db, Opcije)
            .Handle(new GetTekucaSedmicaQuery(), default);

        var (godina, sedmica) = Sada();
        var ocekivanaIsporuka = TimeZoneInfo.ConvertTimeToUtc(
            System.Globalization.ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday), SedmicaHelper.Zona);

        Assert.Equal(ocekivanaIsporuka, dto.DatumIsporuke);
        Assert.Equal(DateTimeKind.Utc, dto.DatumIsporuke.Kind);
    }

    /// <summary>
    /// Poslije roka checkout prodaje za narednu sedmicu (SedmicaZaNarudzbu), pa odbrojavanje
    /// na početnoj treba rok te naredne sedmice. Računa se istim DatumIsporuke kao tekući,
    /// samo za sedmicu više — zato je razlika tačno sedam dana.
    /// </summary>
    [Fact]
    public async Task Sljedeci_krug_je_tacno_sedam_dana_poslije_tekuceg()
    {
        using var t = new TestDb();
        t.Db.DeliverySlots.Add(new DeliverySlot
        {
            DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48,
        });
        t.Db.SaveChanges();

        var dto = await new GetTekucaSedmicaHandler(t.Db, Opcije)
            .Handle(new GetTekucaSedmicaQuery(), default);

        // Sedam dana po LOKALNOM kalendaru — preko prelaza ljetno/zimsko vrijeme je to 7 dana ± 1 h u UTC-u.
        DateTime L(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, SedmicaHelper.Zona);
        Assert.Equal(L(dto.DatumIsporuke).AddDays(7), L(dto.SljedecaIsporuka));
        Assert.Equal(L(dto.RokNarucivanja).AddDays(7), L(dto.SljedeciRok));
        Assert.Equal(DateTimeKind.Utc, dto.SljedeciRok.Kind);
    }

    /// <summary>
    /// 2026. ima 53 ISO sedmice. Sljedeći krug poslije 53. je prva sedmica 2027, a ne
    /// „sedmica 54" — pin za par Pomjeri + DatumIsporuke koji handler koristi.
    /// </summary>
    [Fact]
    public void Sljedeci_krug_prelazi_granicu_godine()
    {
        var (g, s) = SedmicaHelper.Pomjeri(2026, 53, 1);
        var isporuka = SedmicaHelper.DatumIsporuke(g, s, DayOfWeek.Thursday, "16:00");

        Assert.Equal((2027, 1), (g, s));
        // 16:00 po Sarajevu zimi (UTC+1) = 15:00 UTC.
        Assert.Equal(new DateTime(2027, 1, 7, 15, 0, 0, DateTimeKind.Utc), isporuka);
    }
}
