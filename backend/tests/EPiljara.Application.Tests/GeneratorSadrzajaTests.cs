using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class GeneratorSadrzajaTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije =
        Options.Create(new KorpiceOpcije());

    /// <summary>Korpica + kategorija + proizvođač + N artikala u bazenu.</summary>
    private static Product Postavi(TestDb t, int[] mjeseciArtikala, int brojArtikala = 10, decimal stock = 100m)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
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
        var cilj = korpica.Price * new KorpiceOpcije().KoeficijentVrijednosti;

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
