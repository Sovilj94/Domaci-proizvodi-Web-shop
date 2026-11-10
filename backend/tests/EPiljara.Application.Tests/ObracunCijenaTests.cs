using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Obračun cijene korpice: tipičan sadržaj mjeseca (glavne kulture + prosjek osnovnih do minimuma), nabavno
/// sa kutijom → najniža cijena za zadatu maržu; vrijednost u piljari → najviša cijena da budemo povoljniji.
/// Novembar, Mala korpica povrća 25 KM, kutija 1 KM, minimum 4 artikla:
///   Bundeva (glavna u novembru) 1 kom: 4 KM, nabavna 2,40, piljara 5
///   4 osnovna artikla 1 kg: 2 KM, nabavna 1,20, piljara 2,50  → 3 mjesta po prosjeku
///   skupa dopuna (prioritet 2), opciona kultura i artikal bez nabavne cijene ne ulaze u tipičan sadržaj
/// Nabavno 2,40 + 3 × 1,20 = 6,00; naša vrijednost 4 + 3 × 2 = 10; piljara 5 + 3 × 2,50 = 12,50.
/// </summary>
public class ObracunCijenaTests
{
    private static (TestDb T, Product Korpica) Postavi()
    {
        var t = new TestDb();
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Gazdinstvo Petrović", Village = "Stričići" };
        t.Db.AddRange(kat, proiz);
        var korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 25m, Unit = Unit.Kom, CategoryId = kat.Id,
            ProducerId = proiz.Id, Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
            AmbalazaCijena = 1m, Stock = 10, IsAvailable = true,
        };
        t.Db.Products.Add(korpica);
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = kat.Id });

        Product Artikal(string naziv, decimal cijena, decimal? nabavna, decimal? piljara, Action<Product>? x = null)
        {
            var p = new Product
            {
                Name = naziv, Slug = naziv.ToLowerInvariant().Replace(' ', '-'), Price = cijena, Unit = Unit.Kg, CategoryId = kat.Id,
                ProducerId = proiz.Id, Stock = 0, MozeUKorpicu = true, IsAvailable = true, NabavnaCijena = nabavna,
                CijenaPiljara = piljara, KolicinaMala = 1m,
            };
            x?.Invoke(p);
            t.Db.Products.Add(p);
            return p;
        }
        Artikal("Bundeva", 4m, 2.4m, 5m, p => { p.Unit = Unit.Kom; p.Mjeseci = [9, 10, 11]; p.MjeseciGlavni = [11]; p.PrioritetUKorpici = 1; });
        for (var i = 1; i <= 4; i++) Artikal($"Osnovno {i}", 2m, 1.2m, 2.5m);
        Artikal("Skupa dopuna", 10m, 6m, 12m, p => p.PrioritetUKorpici = 2);
        Artikal("Kopriva", 3m, 0.5m, 4m, p => p.Opciono = true);
        Artikal("Bez nabavne", 2m, null, 3m);
        Artikal("Ljetno", 2m, 1m, 3m, p => p.Mjeseci = [6, 7]);
        t.Db.SaveChanges();
        return (t, korpica);
    }

    private static IOptions<KorpiceOpcije> Opcije()
    {
        var o = TestOpcije.Korpice();
        o.MinimalanBrojArtikala = new() { [VelicinaKorpice.Mala] = 4, [VelicinaKorpice.Srednja] = 4, [VelicinaKorpice.Velika] = 4 };
        return Options.Create(o);
    }

    [Fact]
    public async Task Tipican_sadrzaj_daje_nabavno_nasu_vrijednost_i_vrijednost_u_piljari()
    {
        var (t, korpica) = Postavi();
        using var _ = t;

        var d = await new GetObracunCijenaKorpicaHandler(t.Db, Opcije())
            .Handle(new GetObracunCijenaKorpicaQuery(Marza: 0.30m, Povoljnije: 0.10m, Mjesec: 11), default);

        var k = Assert.Single(d.Korpice);
        Assert.Equal(korpica.Id, k.Id);
        var m = k.Izabrani;
        Assert.Equal(4, m.Artikala);
        Assert.Equal(1, m.Glavnih);
        Assert.Equal(6.00m, m.Nabavno);
        Assert.Equal(10.00m, m.NasaVrijednost);
        Assert.Equal(12.50m, m.PiljaraVrijednost);
        // (6 + 1 kutija) / (1 − 0,30) = 10,00; piljara − 10 % = 11,25
        Assert.Equal(10.00m, m.MinCijena);
        Assert.Equal(11.25m, m.MaxCijena);
        Assert.True(m.Moguce);
        Assert.Equal(10.0m, k.Preporuka);
        Assert.Equal(1, d.BezNabavne);
        Assert.Equal(0, d.BezPiljare);
    }

    [Fact]
    public async Task Kad_marza_i_povoljnost_ne_mogu_zajedno_obracun_to_kaze()
    {
        var (t, _) = Postavi();
        using var _t = t;

        var d = await new GetObracunCijenaKorpicaHandler(t.Db, Opcije())
            .Handle(new GetObracunCijenaKorpicaQuery(Marza: 0.45m, Povoljnije: 0.10m, Mjesec: 11), default);

        var m = Assert.Single(d.Korpice).Izabrani;
        // 7 / 0,55 = 12,73 → treba bar 13,00 KM, a da budemo 10 % jeftiniji od piljare najviše 11,25.
        Assert.Equal(12.73m, m.MinCijena);
        Assert.False(m.Moguce);
        Assert.Equal(13.0m, Assert.Single(d.Korpice).Preporuka);
    }

    [Fact]
    public async Task Postavljena_cijena_mijenja_cijenu_korpice()
    {
        var (t, korpica) = Postavi();
        using var _ = t;

        await new PostaviCijenuKorpiceHandler(t.Db).Handle(new PostaviCijenuKorpiceCommand(korpica.Id, 27.5m), default);

        Assert.Equal(27.5m, (await t.Db.Products.FindAsync(korpica.Id))!.Price);
    }
}
