using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Scenario ima korpicu sa CiljnaVrijednost = 7.00 (Krompir 2kg×2 + Paprika 1kg×3) i
/// MinimalanBrojArtikala[Mala] = 2 — vidi Scenario.NapraviAsync.
/// </summary>
public class MojaKorpicaIzmjenaTests
{
    [Fact]
    public async Task Zamjena_u_okviru_budzeta_prolazi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        // Krompir (2kg = 4.00) + Sir (1 kom = 3.00) → 7.00 = 100%
        var rezultat = await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);

        Assert.Equal(2, rezultat.Stavke.Count);
        var sir = rezultat.Stavke.Single(s => s.Naziv == "Sir");
        Assert.True(sir.DodaoKupac);
        Assert.Equal(1m, sir.Kolicina);          // dodat artikal dobija 1 u svojoj jedinici
        var krompir = rezultat.Stavke.Single(s => s.Naziv == "Krompir");
        Assert.False(krompir.DodaoKupac);
        Assert.Equal(2m, krompir.Kolicina);      // iz šablona zadržava šablonsku količinu
    }

    [Theory]
    [InlineData(94.9)]
    [InlineData(100.1)]
    public async Task Van_opsega_budzeta_se_odbija(double procenat)
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniNaProcenatAsync(id, procenat));

        Assert.Contains(ex.Code, new[] { "budzet_premali", "budzet_preveliki" });
    }

    [Fact]
    public async Task Ispod_minimalnog_broja_artikala_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.SkupiArtikalId]));   // jedan artikal, tačno 7.00

        Assert.Equal("premalo_artikala", ex.Code);
    }

    [Fact]
    public async Task Duplirani_artikal_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.KrompirId]));

        Assert.Equal("duplirani_artikal", ex.Code);
    }

    [Fact]
    public async Task Nepodoban_artikal_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.VanSezoneId]));

        Assert.Equal("artikal_nije_podoban", ex.Code);
    }

    [Fact]
    public async Task Narudzba_koja_se_pakuje_se_ne_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == dto.Id);
        o.Status = OrderStatus.Confirmed;
        o.Status = OrderStatus.Packing;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId]));

        Assert.Equal("korpica_se_pakuje", ex.Code);
    }

    [Fact]
    public async Task Tudja_korpica_daje_403()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId], kao: Guid.NewGuid()));
    }

    [Fact]
    public async Task Izmjena_se_vidi_na_pakerskoj_listi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Contains(lista.Stavke, s => s.Artikal == "Sir");
        Assert.DoesNotContain(lista.Stavke, s => s.Artikal == "Paprika");
    }

    [Fact]
    public async Task Stari_redovi_se_ne_gomilaju()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        var broj = await t.Db.StavkeKorpiceNarudzbe.AsNoTracking()
            .CountAsync(s => s.OrderItemId == id);

        Assert.Equal(2, broj);
    }
}
