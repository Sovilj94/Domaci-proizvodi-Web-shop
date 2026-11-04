using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

public class PostavkeKorpicaTests
{
    private sealed record Osnova(TestDb T, Category Povrce, Category Voce, Product Mala, Product Krompir, Product Jaja);

    private static async Task<Osnova> PostaviAsync()
    {
        var t = new TestDb();
        var povrce = new Category { Name = "Povrće", Slug = "povrce" };
        var voce = new Category { Name = "Voće", Slug = "voce" };
        var proiz = new Producer { Name = "Marko Marković", Village = "Stričići" };
        t.Db.AddRange(povrce, voce, proiz);
        var mala = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala", Price = 25m, Unit = Unit.Kom, CategoryId = povrce.Id,
            ProducerId = proiz.Id, Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
        };
        var krompir = new Product { Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg, CategoryId = povrce.Id, ProducerId = proiz.Id };
        var jaja = new Product { Name = "Jaja", Slug = "jaja", Price = 0.5m, Unit = Unit.Kom, CategoryId = povrce.Id, ProducerId = proiz.Id };
        t.Db.Products.AddRange(mala, krompir, jaja);
        t.Db.KorpicaKategorije.Add(new KorpicaKategorija { TipKorpice = TipKorpice.Povrce, CategoryId = povrce.Id });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
        return new Osnova(t, povrce, voce, mala, krompir, jaja);
    }

    [Fact]
    public async Task Pregled_daje_svaku_vrstu_sa_korpicama_i_kategorijama()
    {
        var o = await PostaviAsync();
        using var _ = o.T;

        var p = await new GetPostavkeKorpicaHandler(o.T.Db).Handle(new GetPostavkeKorpicaQuery(), default);

        Assert.Equal(Enum.GetValues<TipKorpice>().Length, p.Tipovi.Count);
        var povrce = p.Tipovi.Single(x => x.Tip == "povrce");
        Assert.Equal("mala", Assert.Single(povrce.Korpice).Velicina);
        Assert.Equal([o.Povrce.Id], povrce.KategorijeIds);
        Assert.Empty(p.Tipovi.Single(x => x.Tip == "voce").KategorijeIds);
    }

    [Fact]
    public async Task Cuvanje_vrste_zamjenjuje_kategorije_i_mijenja_cijenu_i_ponudu()
    {
        var o = await PostaviAsync();
        using var _ = o.T;

        var p = await new SacuvajTipKorpiceHandler(o.T.Db).Handle(new SacuvajTipKorpiceCommand(
            "povrce", [o.Voce.Id], [new KorpicaUnos(o.Mala.Id, 27m, false)]), default);

        Assert.Equal([o.Voce.Id], p.Tipovi.Single(x => x.Tip == "povrce").KategorijeIds);
        var k = await o.T.Db.Products.AsNoTracking().SingleAsync(x => x.Id == o.Mala.Id);
        Assert.Equal(27m, k.Price);
        Assert.False(k.IsAvailable);
    }

    [Fact]
    public async Task Ambalaza_korpice_se_cuva_a_bez_vrijednosti_ostaje()
    {
        var o = await PostaviAsync();
        var h = new SacuvajTipKorpiceHandler(o.T.Db);

        var p = await h.Handle(new SacuvajTipKorpiceCommand("povrce", [], [new KorpicaUnos(o.Mala.Id, 25m, true, 1.20m)]), default);
        Assert.Equal(1.20m, p.Tipovi.Single(x => x.Tip == "povrce").Korpice.Single(k => k.Id == o.Mala.Id).Ambalaza);

        p = await h.Handle(new SacuvajTipKorpiceCommand("povrce", [], [new KorpicaUnos(o.Mala.Id, 25m, true)]), default);
        Assert.Equal(1.20m, p.Tipovi.Single(x => x.Tip == "povrce").Korpice.Single(k => k.Id == o.Mala.Id).Ambalaza);

        p = await h.Handle(new SacuvajTipKorpiceCommand("povrce", [], [new KorpicaUnos(o.Mala.Id, 25m, true, 0m)]), default);
        Assert.Null(p.Tipovi.Single(x => x.Tip == "povrce").Korpice.Single(k => k.Id == o.Mala.Id).Ambalaza);
    }

    [Fact]
    public async Task Korpica_druge_vrste_se_ne_moze_mijenjati_kroz_ovu()
    {
        var o = await PostaviAsync();
        using var _ = o.T;

        await Assert.ThrowsAsync<NotFoundException>(() => new SacuvajTipKorpiceHandler(o.T.Db).Handle(
            new SacuvajTipKorpiceCommand("voce", [], [new KorpicaUnos(o.Mala.Id, 27m, true)]), default));
    }

    [Fact]
    public async Task Artikli_se_cuvaju_odjednom_a_komad_ne_moze_na_pola()
    {
        var o = await PostaviAsync();
        using var _ = o.T;

        await new SacuvajArtikleKorpicaHandler(o.T.Db).Handle(new SacuvajArtikleKorpicaCommand(
            [new ArtikalKorpiceUnos(o.Krompir.Id, true, 0, 1.5m, 2m, null)]), default);
        var k = await o.T.Db.Products.AsNoTracking().SingleAsync(x => x.Id == o.Krompir.Id);
        Assert.True(k.MozeUKorpicu);
        Assert.Equal((1.5m, 2m, (decimal?)null), (k.KolicinaMala!.Value, k.KolicinaSrednja!.Value, k.KolicinaVelika));

        var ex = await Assert.ThrowsAsync<DomainException>(() => new SacuvajArtikleKorpicaHandler(o.T.Db).Handle(
            new SacuvajArtikleKorpicaCommand([new ArtikalKorpiceUnos(o.Jaja.Id, true, 0, 2.5m, null, null)]), default));
        Assert.Equal("kolicina_na_komad", ex.Code);
    }
}
