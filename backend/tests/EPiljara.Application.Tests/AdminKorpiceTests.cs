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

    private sealed record Postavka(
        Product Korpica1, Product Korpica2,
        Product Artikal, Product Artikal2, Product Artikal3,
        Producer Proizvodjac);

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

        Product Artikal(string naziv, string slug, decimal cijena) => new()
        {
            Name = naziv, Slug = slug, Price = cijena, Unit = Unit.Kg,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50, MozeUKorpicu = true,
        };

        var k1 = Korpica("mala-povrce", VelicinaKorpice.Mala, 25m);
        var k2 = Korpica("srednja-povrce", VelicinaKorpice.Srednja, 35m);
        var artikal = Artikal("Paradajz", "paradajz", 4m);
        var artikal2 = Artikal("Krompir", "krompir", 2m);
        var artikal3 = Artikal("Luk", "luk", 1.5m);
        t.Db.Products.AddRange(k1, k2, artikal, artikal2, artikal3);
        t.Db.SaveChanges();
        return new Postavka(k1, k2, artikal, artikal2, artikal3, proiz);
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

        // Dva artikla u nacrtu, pa zamjena TREĆIM, potpuno drugim artiklom — tako da
        // "update u mjestu" postojećeg reda (isti ProductId, samo nova količina) ne može
        // slučajno proći kao ispravno "zamijeni u cjelosti". Vidi Task 7, nalaz review-a:
        // test sa istim ProductId ne razlikuje "wholesale replace" od "mutate in place".
        var s = new SedmicniSadrzaj
        {
            KorpicaId = p.Korpica1.Id, Godina = 2026, Sedmica = 41, Status = StatusSadrzaja.Nacrt,
        };
        s.Stavke.Add(new StavkaSadrzaja { ProductId = p.Artikal.Id, Kolicina = 2m, JedinicnaCijena = p.Artikal.Price });
        s.Stavke.Add(new StavkaSadrzaja { ProductId = p.Artikal2.Id, Kolicina = 1m, JedinicnaCijena = p.Artikal2.Price });
        t.Db.SedmicniSadrzaji.Add(s);
        t.Db.SaveChanges();
        var stareStavkeIdjevi = s.Stavke.Select(x => x.Id).ToList();

        await new IzmijeniNacrtHandler(t.Db).Handle(
            new IzmijeniNacrtCommand(s.Id, [new StavkaUnos(p.Artikal3.Id, 5m)]), default);

        var preostale = await t.Db.StavkeSadrzaja.AsNoTracking().ToListAsync();
        var stavka = Assert.Single(preostale);
        Assert.Equal(p.Artikal3.Id, stavka.ProductId);
        Assert.Equal(5m, stavka.Kolicina);
        Assert.DoesNotContain(preostale, x => x.ProductId == p.Artikal.Id || x.ProductId == p.Artikal2.Id);
        // Red je nov, ne recikliran: pravo "zamijeni u cjelosti" briše stare redove i
        // pravi nove, ne prepisuje polja postojećeg reda u mjestu.
        Assert.DoesNotContain(stavka.Id, stareStavkeIdjevi);
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
