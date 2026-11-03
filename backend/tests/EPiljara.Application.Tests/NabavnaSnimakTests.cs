using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba pamti nabavnu cijenu u trenutku nastanka, da zarada za prošle mjesece ne zavisi od
/// današnjih cijena. Snima se centralno (AppDbContext.SaveChangesAsync), pa je ne zaobilazi
/// nijedna putanja: kupac, gost, pretplata, admin, izmjena korpice.
/// </summary>
public class NabavnaSnimakTests
{
    private static async Task PostaviNabavnuAsync(Scenario t, Guid id, decimal? cijena)
    {
        var p = await t.Db.Products.FirstAsync(x => x.Id == id);
        p.NabavnaCijena = cijena;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Nova_narudzba_snima_nabavnu_i_za_spajz_i_za_sadrzaj_korpice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        var spajz = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId);
        Assert.Equal(1.20m, spajz.NabavnaCijena);
        var uKorpici = await t.Db.StavkeKorpiceNarudzbe.SingleAsync(s => s.ProductId == t.KrompirId);
        Assert.Equal(1.20m, uKorpici.NabavnaCijena);
        var korpica = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KorpicaId);
        Assert.Null(korpica.NabavnaCijena); // korpica sama nema nabavnu — nabavlja se sadržaj
    }

    [Fact]
    public async Task Kasnija_promjena_nabavne_ne_mijenja_snimak()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        await t.PosaljiObicanArtikalAsync();
        await PostaviNabavnuAsync(t, t.KrompirId, 9m);

        var spajz = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId);
        Assert.Equal(1.20m, spajz.NabavnaCijena);
    }

    [Fact]
    public async Task Vec_postavljen_snimak_se_ne_prepisuje()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        var o = await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        t.Db.OrderItems.Add(new OrderItem
        {
            OrderId = o.Id, ProductId = t.KrompirId, ProductName = "Krompir", Unit = Domain.Enums.Unit.Kg,
            Quantity = 1m, UnitPrice = 2m, NabavnaCijena = 0.80m,
        });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Contains(await t.Db.OrderItems.Where(i => i.ProductId == t.KrompirId).ToListAsync(),
            i => i.NabavnaCijena == 0.80m);
    }

    [Fact]
    public async Task Artikal_bez_nabavne_ostavlja_snimak_prazan()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, null);
        await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Null((await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId)).NabavnaCijena);
    }
}
