using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class AdminKorpiceTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije = Options.Create(new KorpiceOpcije());

    private sealed record Postavka(Product Korpica1, Product Korpica2, Product Artikal, Producer Proizvodjac);

    private static Postavka Postavi(TestDb t)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        Product Korpica(string slug, VelicinaKorpice v, decimal cijena) => new()
        {
            Name = $"Korpica {slug}", Slug = slug, Price = cijena, Unit = Unit.Kom,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 30,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = v,
        };

        var k1 = Korpica("mala-povrce", VelicinaKorpice.Mala, 25m);
        var k2 = Korpica("srednja-povrce", VelicinaKorpice.Srednja, 35m);
        var artikal = new Product
        {
            Name = "Paradajz", Slug = "paradajz", Price = 4m, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50, MozeUKorpicu = true,
        };
        t.Db.Products.AddRange(k1, k2, artikal);
        t.Db.SaveChanges();
        return new Postavka(k1, k2, artikal, proiz);
    }

    private static SedmicniSadrzaj Sadrzaj(TestDb t, Product korpica, Product artikal, decimal kolicina, StatusSadrzaja status)
    {
        var s = new SedmicniSadrzaj
        {
            KorpicaId = korpica.Id, Godina = 2026, Sedmica = 41, Status = status,
        };
        s.Stavke.Add(new StavkaSadrzaja { ProductId = artikal.Id, Kolicina = kolicina, JedinicnaCijena = artikal.Price });
        t.Db.SedmicniSadrzaji.Add(s);
        t.Db.SaveChanges();
        return s;
    }

    [Fact]
    public async Task Pregled_prikazuje_i_korpice_bez_sadrzaja()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        var rez = await new GetSadrzajPregledHandler(t.Db, Opcije)
            .Handle(new GetSadrzajPregledQuery(2026, 41), default);

        Assert.Equal(2, rez.Count);
        var bez = Assert.Single(rez, r => r.KorpicaId == p.Korpica2.Id);
        Assert.Null(bez.SadrzajId);
        Assert.Equal("Nema", bez.Status);
    }

    [Fact]
    public async Task Admin_moze_ucitati_nacrt_sa_stavkama()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        var s = Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        // Bez ovoga EF fixup iz dijeljenog konteksta popunjava st.Product i test
        // prolazi i bez Include-a u handleru — vidi Task 7, Korekcija 2.
        t.Db.ChangeTracker.Clear();

        var dto = await new GetSadrzajZaIzmjenuHandler(t.Db)
            .Handle(new GetSadrzajZaIzmjenuQuery(s.Id), default);

        Assert.Equal("Nacrt", dto.Status);
        var stavka = Assert.Single(dto.Stavke);
        Assert.Equal("Paradajz", stavka.Naziv);
        Assert.Equal(2m, stavka.Kolicina);
    }

    [Fact]
    public async Task Izmjena_nacrta_zamjenjuje_stavke_u_cjelosti()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        var s = Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        await new IzmijeniNacrtHandler(t.Db).Handle(
            new IzmijeniNacrtCommand(s.Id, [new StavkaUnos(p.Artikal.Id, 5m)]), default);

        var stavka = Assert.Single(await t.Db.StavkeSadrzaja.AsNoTracking().ToListAsync());
        Assert.Equal(5m, stavka.Kolicina);
    }

    [Fact]
    public async Task Objavljen_sadrzaj_se_ne_moze_mijenjati()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        var s = Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Objavljen);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new IzmijeniNacrtHandler(t.Db).Handle(
                new IzmijeniNacrtCommand(s.Id, [new StavkaUnos(p.Artikal.Id, 5m)]), default));

        Assert.Equal("sadrzaj_nije_nacrt", ex.Code);
    }

    [Fact]
    public async Task Korpica_se_ne_moze_dodati_kao_stavka()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        var s = Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new IzmijeniNacrtHandler(t.Db).Handle(
                new IzmijeniNacrtCommand(s.Id, [new StavkaUnos(p.Korpica2.Id, 1m)]), default));

        Assert.Equal("korpica_u_korpici", ex.Code);
    }

    [Fact]
    public async Task Kolicina_mora_biti_veca_od_nule()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        var s = Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new IzmijeniNacrtHandler(t.Db).Handle(
                new IzmijeniNacrtCommand(s.Id, [new StavkaUnos(p.Artikal.Id, 0m)]), default));

        Assert.Equal("kolicina_nevalidna", ex.Code);
    }

    [Fact]
    public async Task Pakerska_lista_sabira_isti_artikal_preko_vise_korpica()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Objavljen);
        Sadrzaj(t, p.Korpica2, p.Artikal, 3.5m, StatusSadrzaja.Objavljen);

        // Bez ovoga EF fixup popunjava Product/Producer iz dijeljenog konteksta i test
        // prolazi i bez Include-a u handleru — vidi Task 7, Korekcija 2.
        t.Db.ChangeTracker.Clear();

        var lista = await new GetPakerskaListaHandler(t.Db)
            .Handle(new GetPakerskaListaQuery(2026, 41), default);

        var red = Assert.Single(lista);
        Assert.Equal("Paradajz", red.Artikal);
        Assert.Equal(5.5m, red.Kolicina);
        Assert.Equal("Mujo Mujić", red.Proizvodjac);
    }

    [Fact]
    public async Task Pakerska_lista_ignorise_nacrte()
    {
        using var t = new TestDb();
        var p = Postavi(t);
        Sadrzaj(t, p.Korpica1, p.Artikal, 2m, StatusSadrzaja.Nacrt);

        var lista = await new GetPakerskaListaHandler(t.Db)
            .Handle(new GetPakerskaListaQuery(2026, 41), default);

        Assert.Empty(lista);
    }
}
