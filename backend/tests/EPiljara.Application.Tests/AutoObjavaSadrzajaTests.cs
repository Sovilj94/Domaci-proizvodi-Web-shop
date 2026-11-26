using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Poslije roka posao sam sastavlja i objavljuje sadržaj sljedeće sedmice (spec 2026-11-26, odluka 1).
/// Bez DeliverySlot-a važi rezervni rok iz TestOpcije: petak 16:00, 48 h ranije.
/// </summary>
public class AutoObjavaSadrzajaTests
{
    /// <summary>Srijeda 2. 12. 2026. prije roka (sr 16:00) → ciljna sedmica 2026-W49, isporuka 4. 12.</summary>
    private static readonly DateTime Decembar = new(2026, 12, 2, 10, 0, 0, DateTimeKind.Utc);
    /// <summary>Ponedjeljak 4. 5. 2026. → ciljna sedmica 2026-W19, isporuka 8. 5.</summary>
    private static readonly DateTime Maj = new(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);

    private static IOptions<KorpiceOpcije> Opcije(int? sezonaOd = null, int? sezonaDo = null)
    {
        var o = TestOpcije.Korpice();
        o.MinimalanBrojArtikala = new() { [VelicinaKorpice.Mala] = 1, [VelicinaKorpice.Srednja] = 1, [VelicinaKorpice.Velika] = 1 };
        o.SezonaVocaOd = sezonaOd;
        o.SezonaVocaDo = sezonaDo;
        return Options.Create(o);
    }

    private static AutoObjaviSadrzajeHandler Handler(TestDb t, IOptions<KorpiceOpcije> o) =>
        new(t.Db, o, NullLogger<AutoObjaviSadrzajeHandler>.Instance);

    /// <summary>Povrćna i voćna mala korpica, svaka sa 10 artikala cijele godine u svojoj kategoriji.</summary>
    private static (Product Povrce, Product Voce, List<Product> Voca) Postavi(TestDb t)
    {
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
        t.Db.Producers.Add(proiz);

        Product Korpica(TipKorpice tip, string naziv, out List<Product> artikli)
        {
            var kat = new Category { Name = naziv, Slug = naziv.ToLowerInvariant() };
            t.Db.Categories.Add(kat);
            t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = tip, CategoryId = kat.Id });
            var k = new Product
            {
                Name = $"Mala korpica {naziv}", Slug = $"mala-{naziv.ToLowerInvariant()}", Price = 25m,
                Unit = Unit.Kom, CategoryId = kat.Id, ProducerId = proiz.Id,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = tip, Velicina = VelicinaKorpice.Mala,
            };
            t.Db.Products.Add(k);
            artikli = Enumerable.Range(0, 10).Select(i => new Product
            {
                Name = $"{naziv} {i}", Slug = $"{naziv.ToLowerInvariant()}-{i}", Price = 4m, Unit = Unit.Kg,
                CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 100m, MozeUKorpicu = true, Mjeseci = [],
            }).ToList();
            t.Db.Products.AddRange(artikli);
            return k;
        }

        var povrce = Korpica(TipKorpice.Povrce, "Povrce", out _);
        var voce = Korpica(TipKorpice.Voce, "Voce", out var voca);
        t.Db.SaveChanges();
        return (povrce, voce, voca);
    }

    private static (int G, int S) Cilj(DateTime sada) =>
        SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Friday, "16:00", 48, sada);

    [Fact]
    public async Task Nova_sedmica_bez_sadrzaja_se_sastavi_i_objavi()
    {
        using var t = new TestDb();
        Postavi(t);

        var r = await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        var (g, s) = Cilj(Decembar);
        (r.Godina, r.Sedmica).Should().Be((g, s));
        r.Objavljeno.Should().Be(2);
        var sadrzaji = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).Where(x => x.Godina == g && x.Sedmica == s).ToListAsync();
        sadrzaji.Should().HaveCount(2);
        sadrzaji.Should().OnlyContain(x => x.Status == StatusSadrzaja.Objavljen && x.Stavke.Count > 0);
    }

    [Fact]
    public async Task Objavljen_sadrzaj_se_ne_dira()
    {
        using var t = new TestDb();
        var (povrce, voce, _) = Postavi(t);
        var o = Opcije();
        await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);
        t.Db.ChangeTracker.Clear();
        var prije = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).AsNoTracking().ToListAsync();

        var r = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        r.Objavljeno.Should().Be(0);
        var poslije = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).AsNoTracking().ToListAsync();
        poslije.Select(x => x.Id).Should().BeEquivalentTo(prije.Select(x => x.Id));
        poslije.SelectMany(x => x.Stavke.Select(st => (st.ProductId, st.Kolicina)))
            .Should().BeEquivalentTo(prije.SelectMany(x => x.Stavke.Select(st => (st.ProductId, st.Kolicina))));
    }

    [Fact]
    public async Task Nacrt_sa_stavkama_se_objavi_kakav_jeste()
    {
        using var t = new TestDb();
        var (_, voce, voca) = Postavi(t);
        var (g, s) = Cilj(Decembar);
        var nacrt = new SedmicniSadrzaj { KorpicaId = voce.Id, Godina = g, Sedmica = s };
        nacrt.Stavke.Add(new StavkaSadrzaja { ProductId = voca[0].Id, Kolicina = 3m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(nacrt);
        await t.Db.SaveChangesAsync();

        await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        var objavljen = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).SingleAsync(x => x.Id == nacrt.Id);
        objavljen.Status.Should().Be(StatusSadrzaja.Objavljen);
        objavljen.Stavke.Should().ContainSingle().Which.Kolicina.Should().Be(3m);
    }

    [Fact]
    public async Task Prazan_nacrt_se_sastavi_iznova()
    {
        using var t = new TestDb();
        var (_, voce, _) = Postavi(t);
        var (g, s) = Cilj(Decembar);
        t.Db.SedmicniSadrzaji.Add(new SedmicniSadrzaj { KorpicaId = voce.Id, Godina = g, Sedmica = s });
        await t.Db.SaveChangesAsync();

        await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        var sadrzaj = await t.Db.SedmicniSadrzaji.Include(x => x.Stavke).SingleAsync(x => x.KorpicaId == voce.Id);
        sadrzaj.Status.Should().Be(StatusSadrzaja.Objavljen);
        sadrzaj.Stavke.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Voce_van_sezone_se_preskace()
    {
        using var t = new TestDb();
        var (povrce, voce, _) = Postavi(t);

        var r = await Handler(t, Opcije(5, 10)).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        r.Objavljeno.Should().Be(1);
        (await t.Db.SedmicniSadrzaji.AnyAsync(x => x.KorpicaId == voce.Id)).Should().BeFalse();
        (await t.Db.SedmicniSadrzaji.SingleAsync(x => x.KorpicaId == povrce.Id)).Status.Should().Be(StatusSadrzaja.Objavljen);
    }

    [Fact]
    public async Task Drugi_prolaz_ne_radi_nista()
    {
        using var t = new TestDb();
        Postavi(t);
        var o = Opcije();
        await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);
        var broj = await t.Db.SedmicniSadrzaji.CountAsync();

        var r = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        r.Objavljeno.Should().Be(0);
        (await t.Db.SedmicniSadrzaji.CountAsync()).Should().Be(broj);
    }

    [Fact]
    public async Task Pad_jedne_korpice_ne_zaustavlja_ostale()
    {
        using var t = new TestDb();
        var (povrce, voce, voca) = Postavi(t);
        var (g, s) = Cilj(Decembar);
        var nacrt = new SedmicniSadrzaj { KorpicaId = voce.Id, Godina = g, Sedmica = s };
        nacrt.Stavke.Add(new StavkaSadrzaja { ProductId = voca[0].Id, Kolicina = 3m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(nacrt);
        voca[0].IsDeleted = true;
        await t.Db.SaveChangesAsync();

        var r = await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        r.Objavljeno.Should().Be(1);
        (await t.Db.SedmicniSadrzaji.SingleAsync(x => x.Id == nacrt.Id)).Status.Should().Be(StatusSadrzaja.Nacrt);
        (await t.Db.SedmicniSadrzaji.SingleAsync(x => x.KorpicaId == povrce.Id)).Status.Should().Be(StatusSadrzaja.Objavljen);
    }

    [Fact]
    public async Task Neaktivna_korpica_se_preskace()
    {
        using var t = new TestDb();
        var (povrce, voce, _) = Postavi(t);
        voce.IsAvailable = false;
        await t.Db.SaveChangesAsync();

        var r = await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        r.Objavljeno.Should().Be(1);
        (await t.Db.SedmicniSadrzaji.AnyAsync(x => x.KorpicaId == voce.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Prva_vocna_u_sezoni()
    {
        using var t = new TestDb();
        Postavi(t);
        var o = Opcije(5, 10);

        var prvi = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj), default);
        var drugi = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj), default);

        prvi.PrvaVocnaUSezoni.Should().BeTrue();
        drugi.PrvaVocnaUSezoni.Should().BeFalse();
    }

    [Fact]
    public async Task Voce_objavljeno_javlja_i_u_prolazu_koji_nista_ne_objavi()
    {
        using var t = new TestDb();
        Postavi(t);
        var o = Opcije(5, 10);

        var prvi = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj), default);
        var drugi = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj), default);
        var zima = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        prvi.VoceObjavljeno.Should().BeTrue();
        drugi.VoceObjavljeno.Should().BeTrue();
        zima.VoceObjavljeno.Should().BeFalse();
    }

    [Fact]
    public async Task Rucno_objavljeno_voce_van_sezone_ne_javlja_prijavljenima()
    {
        using var t = new TestDb();
        var (_, voce, voca) = Postavi(t);
        var (g, s) = Cilj(Decembar);
        // Admin je ručno objavio voće u decembru — dozvoljeno, ali sezona i dalje važi.
        var sadrzaj = new SedmicniSadrzaj { KorpicaId = voce.Id, Godina = g, Sedmica = s, Status = StatusSadrzaja.Objavljen };
        sadrzaj.Stavke.Add(new StavkaSadrzaja { ProductId = voca[0].Id, Kolicina = 3m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(sadrzaj);
        await t.Db.SaveChangesAsync();

        var r = await Handler(t, Opcije(5, 10)).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        r.VoceObjavljeno.Should().BeFalse();
    }

    [Fact]
    public async Task Korpica_poslije_one_koja_pukne_se_i_dalje_objavi()
    {
        using var t = new TestDb();
        var (povrce, voce, _) = Postavi(t);
        var (g, s) = Cilj(Decembar);
        // Povrće ide prvo (TipKorpice se poredi kao tekst: "Povrce" < "Voce"), pa ovdje pada PRVA korpica.
        var artikal = new Product
        {
            Name = "Obrisano", Slug = "obrisano", Price = 4m, Unit = Unit.Kg, CategoryId = povrce.CategoryId,
            ProducerId = povrce.ProducerId, Stock = 100m, MozeUKorpicu = true, Mjeseci = [], IsDeleted = true,
        };
        t.Db.Products.Add(artikal);
        var nacrt = new SedmicniSadrzaj { KorpicaId = povrce.Id, Godina = g, Sedmica = s };
        nacrt.Stavke.Add(new StavkaSadrzaja { ProductId = artikal.Id, Kolicina = 3m, JedinicnaCijena = 4m });
        t.Db.SedmicniSadrzaji.Add(nacrt);
        await t.Db.SaveChangesAsync();

        var r = await Handler(t, Opcije()).Handle(new AutoObjaviSadrzajeCommand(Decembar), default);

        t.Db.ChangeTracker.Clear();
        r.Objavljeno.Should().Be(1);
        (await t.Db.SedmicniSadrzaji.SingleAsync(x => x.Id == nacrt.Id)).Status.Should().Be(StatusSadrzaja.Nacrt);
        (await t.Db.SedmicniSadrzaji.SingleAsync(x => x.KorpicaId == voce.Id)).Status.Should().Be(StatusSadrzaja.Objavljen);
    }

    [Fact]
    public async Task Vocna_sedmica_poslije_vocne_nije_prva()
    {
        using var t = new TestDb();
        Postavi(t);
        var o = Opcije(5, 10);
        await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj), default);

        var sljedeca = await Handler(t, o).Handle(new AutoObjaviSadrzajeCommand(Maj.AddDays(7)), default);

        sljedeca.Objavljeno.Should().Be(2);
        sljedeca.PrvaVocnaUSezoni.Should().BeFalse();
    }
}
