using EPiljara.Application.Features.Catalog;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Korpica nema svoju zalihu: koliko ih može da se proda izvodi se iz sadržaja sedmice
/// (Scenario: Krompir 2 kg + Paprika 1 kg) i slobodnog stanja artikala, a slobodno je stanje
/// minus ono što je obećano u korpicama te sedmice.
/// </summary>
public class ZaliheTests
{
    private static async Task PostaviStanjeAsync(Scenario t, Guid productId, decimal stanje)
    {
        var p = await t.Db.Products.FirstAsync(x => x.Id == productId);
        p.Stock = stanje;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
    }

    private static async Task<EPiljara.Application.DTOs.KorpicaDto> KorpicaAsync(Scenario t)
    {
        t.Db.ChangeTracker.Clear();
        var lista = await new GetKorpiceHandler(
                t.Db, Options.Create(TestOpcije.Pretplate()), Options.Create(t.Opcije))
            .Handle(new GetKorpiceQuery(null), default);
        return lista.Single(k => k.Id == t.KorpicaId);
    }

    [Fact]
    public async Task Kapacitet_se_racuna_iz_sadrzaja_i_pada_sa_svakom_prodanom_korpicom()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviStanjeAsync(t, t.KrompirId, 20m);   // 20 / 2 kg = 10 korpica
        await PostaviStanjeAsync(t, t.PaprikaId, 30m);   // 30 / 1 kg = 30 korpica

        var prije = await KorpicaAsync(t);
        Assert.Equal(10m, prije.Stock);
        Assert.Equal("Krompir", prije.UskoGrlo);
        Assert.True(prije.Dostupna);

        await t.PosaljiKorpicuAsync(3m);

        Assert.Equal(7m, (await KorpicaAsync(t)).Stock);
    }

    [Fact]
    public async Task Korpica_bez_objavljenog_sadrzaja_nije_dostupna()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var k = await KorpicaAsync(t);

        Assert.Equal(0m, k.Stock);
        Assert.False(k.Dostupna);
    }

    [Fact]
    public async Task Narudzba_preko_kapaciteta_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviStanjeAsync(t, t.PaprikaId, 2m);    // tačno za dvije korpice

        await t.PosaljiKorpicuAsync(2m);
        t.Db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<DomainException>(() => t.PosaljiKorpicuAsync(1m));

        Assert.Equal("korpica_rasprodata", ex.Code);
        Assert.Contains("Paprika", ex.Message);
        Assert.False((await KorpicaAsync(t)).Dostupna);
    }

    [Fact]
    public async Task Spajz_ne_prodaje_ono_sto_je_obecano_korpicama()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviStanjeAsync(t, t.KrompirId, 5m);
        await t.PosaljiKorpicuAsync(2m);                 // 2 × 2 kg krompira obećano
        t.Db.ChangeTracker.Clear();

        await t.PosaljiObicanArtikalAsync();             // 1 kg — tačno ono što je ostalo
        t.Db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<DomainException>(() => t.PosaljiObicanArtikalAsync());
        Assert.Equal("nema_zalihe", ex.Code);

        t.Db.ChangeTracker.Clear();
        var katalog = await new GetProductsHandler(t.Db, Options.Create(t.Opcije), TestOpcije.ProdavnicaO())
            .Handle(new GetProductsQuery(null, null, null), default);
        Assert.Equal(0m, katalog.Single(p => p.Id == t.KrompirId).Stock);
    }

    [Fact]
    public async Task Kupac_ne_moze_dodati_artikal_koji_je_druga_korpica_vec_uzela()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await PostaviStanjeAsync(t, t.SirId, 1m);        // jedan sir za cijelu sedmicu

        var prva = await t.OrderItemIdAsync((await t.PosaljiKorpicuAsync(1m)).Id);
        var druga = await t.OrderItemIdAsync((await t.PosaljiKorpicuAsync(1m)).Id);
        t.Db.ChangeTracker.Clear();

        // Krompir 2 kg (4.00) + Sir 1 kom (3.00) = 7.00, tačno budžet.
        await t.IzmijeniAsync(prva, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<DomainException>(() => t.IzmijeniAsync(druga, [t.KrompirId, t.SirId]));

        Assert.Equal("nema_zalihe", ex.Code);
    }

    [Fact]
    public async Task Kupac_moze_ponovo_sacuvati_korpicu_sa_svojim_artiklom()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await PostaviStanjeAsync(t, t.SirId, 1m);
        var id = await t.OrderItemIdAsync((await t.PosaljiKorpicuAsync(1m)).Id);
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        // Sir je već njegov — njegovi redovi se ne broje kao tuđe obećano.
        var opet = await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        Assert.Contains(opet.Stavke, s => s.Naziv == "Sir");
    }
}
