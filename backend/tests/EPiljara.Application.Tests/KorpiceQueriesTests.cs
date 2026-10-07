using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KorpiceQueriesTests
{
    private static SedmicniSadrzaj Postavi(TestDb t, StatusSadrzaja status, int godina, int sedmica)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
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

        var rez = await new GetKorpiceHandler(t.Db).Handle(new GetKorpiceQuery(null), default);

        var k = Assert.Single(rez);
        Assert.Equal("mala-korpica-povrca", k.Slug);
    }

    [Fact]
    public async Task Lista_se_filtrira_po_tipu()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Objavljen, g, s);

        Assert.Single(await new GetKorpiceHandler(t.Db).Handle(new GetKorpiceQuery("povrce"), default));
        Assert.Empty(await new GetKorpiceHandler(t.Db).Handle(new GetKorpiceQuery("mlijecna"), default));
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

        var dto = await new GetSadrzajKorpiceHandler(t.Db)
            .Handle(new GetSadrzajKorpiceQuery("mala-korpica-povrca"), default);

        var stavka = Assert.Single(dto.Stavke);
        Assert.Equal("Paradajz", stavka.Naziv);
        Assert.Equal("Mujo Mujić", stavka.ProizvodjacNaziv);
        Assert.Equal("Stričići", stavka.ProizvodjacSelo);
        Assert.Equal("kg", stavka.Jedinica);
    }

    [Fact]
    public async Task Nacrt_nije_vidljiv_javnom_upitu()
    {
        using var t = new TestDb();
        var (g, s) = Sada();
        Postavi(t, StatusSadrzaja.Nacrt, g, s);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetSadrzajKorpiceHandler(t.Db)
                .Handle(new GetSadrzajKorpiceQuery("mala-korpica-povrca"), default));
        Assert.Contains("sadrzaj_nije_objavljen", ex.Message);
    }
}
