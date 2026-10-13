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

    // ---------- Ispravke finalnog review-a ----------

    [Fact]
    public async Task Obicna_stavka_svoje_narudzbe_se_ne_moze_mijenjati()
    {
        // Nalaz F2: bez provjere Vrste, PUT uperen u običan red (1 kg krompira) prolazi sve
        // straže — vlasništvo, status, rok, podobnost — a cilj budžeta ispadne 0 pa se OBJE
        // granice iskljuce. Upisani redovi odu na pakersku listu, pomnoženi sa Quantity.
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiObicanArtikalAsync();
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<NotFoundException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SkupiArtikalId, t.SirId]));

        t.Db.ChangeTracker.Clear();
        var broj = await t.Db.StavkeKorpiceNarudzbe.AsNoTracking()
            .CountAsync(s => s.OrderItemId == id);

        Assert.Equal(0, broj);
    }

    [Fact]
    public async Task Korpica_bez_sedmicnog_sadrzaja_se_ne_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        // Sadržaj te sedmice nestane poslije narudžbe. Prikaz u tom stanju pada na 100% da
        // stara narudžba ne obori listu, ali upis mora da ODBIJE: dok je cilj mogao ispasti
        // 0, [Krompir, SkupiArtikal] = 9.00 je prolazilo bez ijedne granice budžeta.
        t.Db.SedmicniSadrzaji.RemoveRange(await t.Db.SedmicniSadrzaji.ToListAsync());
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SkupiArtikalId]));

        Assert.Equal("korpica_bez_sadrzaja", ex.Code);
    }

    [Fact]
    public async Task Poslije_roka_se_ne_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        // Rok se ne čeka — izvodi se iz Order.Godina/Sedmica, pa se pomjera taj ulaz.
        await t.PomjeriSedmicuUProslostAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId]));

        Assert.Equal("rok_istekao", ex.Code);
    }

    [Fact]
    public async Task Donja_granica_budzeta_prolazi()
    {
        // 95.0% od 7.00 je Math.Round(7.00m * 0.95m, 2) = 6.65, i granica je INKLUZIVNA.
        // 6.64 i 7.01 su je dotad okružavali s obje strane, ali je nijedan test nije dotakao.
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        // [Krompir (2kg × 2.00 = 4.00), Luk (1 kom × 2.65)] = 6.65
        var rezultat = await t.IzmijeniNaProcenatAsync(id, 95.0);

        Assert.Equal(6.65m, rezultat.Korpica.TrenutnaVrijednost);
        Assert.Equal(2, rezultat.Stavke.Count);
    }

    [Fact]
    public async Task Zadrzan_artikal_cuva_zamrznutu_cijenu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        // Krompir poskupi POSLIJE narudžbe: 2.00 → 2.40. Cilj (7.00) je zamrznut pri objavi i
        // ne pomjera se. Da izmjena preračunava zadržan red na današnju cijenu, kutija bi
        // vrijedila 2×2.40 + 3.00 = 7.80 i ovaj poziv bi pao na budzet_preveliki.
        var krompir = await t.Db.Products.FirstAsync(p => p.Id == t.KrompirId);
        krompir.Price = 2.40m;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        var red = await t.Db.StavkeKorpiceNarudzbe.AsNoTracking()
            .FirstAsync(s => s.OrderItemId == id && s.ProductId == t.KrompirId);

        Assert.Equal(2.00m, red.JedinicnaCijena);
        Assert.Equal(2m, red.Kolicina);
    }

    [Fact]
    public async Task Zadrzan_artikal_van_sezone_se_i_dalje_moze_sacuvati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        // Paprika je iz šablona i izlazi iz sezone poslije narudžbe. Kupac je ne DODAJE nego
        // zadržava, pa podobnost na nju ne važi — spec je vezuje za „dodati artikal". Inače
        // bi svaki PUT nosio Papriku i cijela kutija bila trajno nesačuvljiva.
        var paprika = await t.Db.Products.FirstAsync(p => p.Id == t.PaprikaId);
        paprika.Mjeseci = [t.VanSezoneMjesec];
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var rezultat = await t.IzmijeniAsync(id, [t.KrompirId, t.PaprikaId]);

        Assert.Equal(2, rezultat.Stavke.Count);
        var p = rezultat.Stavke.Single(s => s.Naziv == "Paprika");
        Assert.Equal(3.00m, p.JedinicnaCijena);
        Assert.Equal(1m, p.Kolicina);
    }

    [Fact]
    public async Task Poruka_o_nepodobnom_artiklu_imenuje_artikal()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.VanSezoneId]));

        // Ekran ove greške prikazuje kroz err.message (fieldErrors je prazan), pa poruka koja
        // ne imenuje artikal kupcu pokazuje ćorsokak bez putokaza.
        Assert.Equal("artikal_nije_podoban", ex.Code);
        Assert.Contains("Kupus", ex.Message);
    }
}
