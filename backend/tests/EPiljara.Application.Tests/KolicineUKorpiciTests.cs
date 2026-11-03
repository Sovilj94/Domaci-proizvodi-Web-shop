using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>Zadate količine po veličini korpice (Product.KolicinaMala/Srednja/Velika).</summary>
public class KolicineUKorpiciTests
{
    [Fact]
    public void Kolicina_za_velicinu_bira_svoju_kolonu_a_nula_ili_null_znaci_nije_zadato()
    {
        var p = new Product { KolicinaMala = 1m, KolicinaSrednja = 1.5m, KolicinaVelika = 0m };
        Assert.Equal(1m, p.KolicinaUKorpici(VelicinaKorpice.Mala));
        Assert.Equal(1.5m, p.KolicinaUKorpici(VelicinaKorpice.Srednja));
        Assert.Null(p.KolicinaUKorpici(VelicinaKorpice.Velika));
        Assert.Null(p.KolicinaUKorpici(null));
    }

    [Fact]
    public async Task Generator_stavlja_zadatu_kolicinu_cijelu_a_ne_racun_iz_vrijednosti()
    {
        using var t = new TestDb();
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);
        t.Db.Products.Add(new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m, Unit = Unit.Kom,
            CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 50, Vrsta = VrstaProizvoda.Korpica,
            TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
        });
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = kat.Id });
        for (var i = 0; i < 12; i++)
            t.Db.Products.Add(new Product
            {
                Name = $"Artikal {i}", Slug = $"artikal-{i}", Price = 2m, Unit = Unit.Kg,
                CategoryId = kat.Id, ProducerId = proiz.Id, Stock = 100m, MozeUKorpicu = true, Mjeseci = [],
                KolicinaMala = 1.5m, KolicinaSrednja = 2m,
            });
        await t.Db.SaveChangesAsync();

        await new GenerisiSedmicniSadrzajHandler(t.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GenerisiSedmicniSadrzajCommand(2026, 45, null), default);

        var stavke = await t.Db.StavkeSadrzaja.ToListAsync();
        Assert.NotEmpty(stavke);
        Assert.All(stavke, s => Assert.Equal(1.5m, s.Kolicina));
        // Cijela pakovanja ne smiju preći cilj.
        Assert.True(stavke.Sum(s => s.Kolicina * s.JedinicnaCijena) <= 25m * TestOpcije.Korpice().KoeficijentVrijednosti);
    }

    [Theory]
    // vrijednostStare, ostalo, cijena, zadato, očekivano — granice 6,65–7,00
    [InlineData(3.0, 4.0, 1.0, 3.0, 3.0)]   // zadato staje tačno
    [InlineData(3.0, 4.0, 1.0, 2.8, 2.8)]   // zadato drži kutiju u 95–100 %
    [InlineData(3.0, 4.0, 1.0, 1.0, 3.0)]   // zadato bi oborilo ispod 95 % → po vrijednosti
    [InlineData(3.0, 4.0, 1.0, 5.0, 3.0)]   // zadato bi probilo cilj → po vrijednosti
    public void Kupceva_zamjena_uzima_zadatu_kolicinu_kad_staje(
        double stara, double ostalo, double cijena, double zadato, double ocekivano) =>
        Assert.Equal((decimal)ocekivano, ZamijeniArtikalUKorpiciHandler.Kolicina(
            (decimal)stara, (decimal)ostalo, (decimal)cijena, 6.65m, 7m, Unit.Kg, (decimal)zadato));
}
