using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Najmanja smislena količina namirnice u korpici — bez nje generator i zamjena prave „mrvice"
/// (0,1 kg bijelog luka, 0,3 kg batata).
/// </summary>
public class NajmanjeUKorpiciTests
{
    private static Product P(Unit u, decimal? najmanje = null, decimal? mala = null) =>
        new() { Name = "x", Slug = "x", Unit = u, NajmanjeUKorpici = najmanje, KolicinaMala = mala };

    [Fact]
    public void Najmanja_je_polje_kad_je_uneseno() =>
        Assert.Equal(1m, P(Unit.Kg, najmanje: 1m, mala: 0.7m).NajmanjaKolicinaUKorpici());

    /// <summary>Bez polja: pola kile za kg — da kupac može smanjiti cveklu sa 1 na 0,5 kg —
    /// ili zadata za malu ako je manja (bijeli luk 0,3). Komad: 1.</summary>
    [Theory]
    [InlineData(0.7, 0.5)]
    [InlineData(2.0, 0.5)]
    [InlineData(0.3, 0.3)]
    public void Bez_polja_najmanja_je_pola_kile_ili_manja_zadata(double mala, double najmanja) =>
        Assert.Equal((decimal)najmanja, P(Unit.Kg, mala: (decimal)mala).NajmanjaKolicinaUKorpici());

    [Theory]
    [InlineData(Unit.Kg, 0.5)]
    [InlineData(Unit.Kom, 1)]
    public void Bez_ikakve_zadate_najmanja_je_pola_kile_ili_komad(Unit u, double najmanja) =>
        Assert.Equal((decimal)najmanja, P(u).NajmanjaKolicinaUKorpici());

    [Theory]
    [InlineData(2.0, 0.5)]   // krompir: korak 0,5 kg
    [InlineData(0.3, 0.3)]   // bijeli luk: korak nije veći od najmanje količine
    public void Korak_za_kg_je_pola_kile_ali_ne_vise_od_najmanje(double najmanje, double korak) =>
        Assert.Equal((decimal)korak, P(Unit.Kg, najmanje: (decimal)najmanje).KorakUKorpici());

    [Fact]
    public void Korak_za_komad_je_jedan() =>
        Assert.Equal(1m, P(Unit.Kom, najmanje: 2m).KorakUKorpici());

    private static async Task<(TestDb t, Product p, Product kom)> Baza()
    {
        var t = new TestDb();
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Marko", Village = "Manjača" };
        var p = new Product { Name = "Batat", Slug = "batat", Price = 5m, Unit = Unit.Kg, Category = kat, Producer = proiz };
        var kom = new Product { Name = "Salata", Slug = "salata", Price = 1.5m, Unit = Unit.Kom, Category = kat, Producer = proiz };
        t.Db.Products.AddRange(p, kom);
        await t.Db.SaveChangesAsync();
        return (t, p, kom);
    }

    private static ArtikalKorpiceUnos Unos(Guid id, decimal? najmanje) =>
        new(id, true, 1, 0.7m, 1m, 1.5m, NajmanjeUKorpici: najmanje);

    [Fact]
    public async Task Postavke_cuvaju_najmanju_kolicinu()
    {
        var (t, p, _) = await Baza();
        using var _t = t;

        await new SacuvajArtikleKorpicaHandler(t.Db).Handle(new([Unos(p.Id, 1m)]), default);

        t.Db.ChangeTracker.Clear();
        Assert.Equal(1m, (await t.Db.Products.SingleAsync(x => x.Id == p.Id)).NajmanjeUKorpici);
    }

    [Fact]
    public void Validator_odbija_nulu_ili_negativnu_najmanju()
    {
        var r = new SacuvajArtikleKorpicaValidator().Validate(new SacuvajArtikleKorpicaCommand([Unos(Guid.NewGuid(), 0m)]));
        Assert.False(r.IsValid);
    }

    [Fact]
    public async Task Komad_ne_moze_imati_decimalnu_najmanju()
    {
        var (t, _, kom) = await Baza();
        using var _t = t;

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new SacuvajArtikleKorpicaHandler(t.Db).Handle(new([new(kom.Id, true, 1, 2m, 3m, 4m, NajmanjeUKorpici: 1.5m)]), default));
        Assert.Equal("kolicina_na_komad", ex.Code);
    }
}
