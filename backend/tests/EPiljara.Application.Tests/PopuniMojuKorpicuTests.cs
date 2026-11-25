using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// „Popuni umjesto mene": dopuna kupčevog nacrta do 95–100 % cilja (7,00 KM u scenariju) bez
/// kršenja marže. Ne čuva ništa — vraća predlog koji kupac pregleda i sačuva.
/// </summary>
public class PopuniMojuKorpicuTests
{
    private static async Task<(Scenario t, Guid id)> Narucena(Action<Scenario>? prije = null)
    {
        var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        prije?.Invoke(t);
        await t.Db.SaveChangesAsync();
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();
        return (t, id);
    }

    private static async Task<decimal> VrijednostAsync(Scenario t, IEnumerable<StavkaKorpiceUnos> stavke)
    {
        var cijene = await t.Db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Price);
        return stavke.Sum(s => s.Kolicina!.Value * cijene[s.ProductId]);
    }

    [Fact]
    public async Task Nacrt_ispod_budzeta_se_dopuni_u_granice()
    {
        var (t, id) = await Narucena();
        using var _ = t;

        var r = await t.PopuniAsync(id, (t.KrompirId, 2m));   // 4,00 od 7,00

        Assert.InRange(await VrijednostAsync(t, r), 6.65m, 7.00m);
        Assert.Contains(r, s => s.ProductId == t.KrompirId);
    }

    [Fact]
    public async Task Popuni_ne_dodaje_artikle_sa_spiska_ne_stavljaj_mi()
    {
        var (t, id) = await Narucena();
        using var _ = t;
        if (!await t.Db.Users.AnyAsync(u => u.Id == t.KupacId))
            t.Db.Users.Add(new User { Id = t.KupacId, Name = "Marko", Email = "marko@test.local", PasswordHash = "x" });
        t.Db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = t.KupacId, ProductId = t.PaprikaId });
        t.Db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = t.KupacId, ProductId = t.SirId });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var r = await t.PopuniAsync(id);

        Assert.NotEmpty(r);
        Assert.DoesNotContain(r, s => s.ProductId == t.PaprikaId || s.ProductId == t.SirId);
    }

    [Fact]
    public async Task Nacrt_preko_budzeta_se_vraca_nepromijenjen()
    {
        var (t, id) = await Narucena();
        using var _ = t;

        var r = await t.PopuniAsync(id, (t.KrompirId, 4m));   // 8,00 > 7,00

        var s = Assert.Single(r);
        Assert.Equal(4m, s.Kolicina);
    }

    /// <summary>Krompir nabavno 3 KM/kg, budžet 7,50: dopuna ne smije gurnuti krompir preko 2,5 kg.</summary>
    [Fact]
    public async Task Popuni_ne_krsi_marzu()
    {
        var (t, id) = await Narucena(s => s.Db.Products.Single(p => p.Id == s.KrompirId).NabavnaCijena = 3m);
        using var _ = t;

        var r = await t.PopuniAsync(id, (t.KrompirId, 2m));

        Assert.True(r.Single(s => s.ProductId == t.KrompirId).Kolicina * 3m <= 7.5m);
    }
}
