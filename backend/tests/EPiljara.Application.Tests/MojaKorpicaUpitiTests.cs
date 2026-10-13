using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class MojaKorpicaUpitiTests
{
    [Fact]
    public async Task Lista_vraca_korpicu_koja_se_jos_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var moje = await t.MojeKorpiceAsync();

        var jedna = Assert.Single(moje);
        Assert.Equal(7m, jedna.TrenutnaVrijednost);   // 2×2 + 1×3, zbir redova narudžbe
        Assert.Equal(2, jedna.BrojArtikala);
    }

    [Fact]
    public async Task CiljnaVrijednost_dolazi_sa_sadrzaja_a_ne_iz_zbira_redova()
    {
        // Scenario upisuje CiljnaVrijednost = 8.00 iako sadržaj sabira 7.00. To nije realno
        // stanje — pri objavi su jednaki — ali je jedini način da se dokaže ODAKLE vrijednost
        // dolazi. Da handler računa cilj iz redova, oba broja bi bila 7.00 i provjera budžeta
        // iz editora bi uvijek davala 100%.
        using var t = await Scenario.NapraviAsync(
            objaviSadrzaj: true, prijavljen: true, ciljnaVrijednost: 8m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var jedna = Assert.Single(await t.MojeKorpiceAsync());

        Assert.Equal(8m, jedna.CiljnaVrijednost);
        Assert.Equal(7m, jedna.TrenutnaVrijednost);
    }

    [Fact]
    public async Task Lista_ne_vraca_tudju_korpicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var moje = await t.MojeKorpiceAsync(kao: Guid.NewGuid());

        Assert.Empty(moje);
    }

    [Fact]
    public async Task Lista_ne_vraca_korpicu_narudzbe_koja_se_vec_pakuje()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == dto.Id);
        o.Status = OrderStatus.Confirmed;
        o.Status = OrderStatus.Packing;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Empty(await t.MojeKorpiceAsync());
    }

    [Fact]
    public async Task Detalji_nose_stavke_i_ponudu_sa_oznakom_kategorije()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var d = await t.MojaKorpicaAsync(await t.OrderItemIdAsync(dto.Id));

        Assert.Equal(2, d.Stavke.Count);
        Assert.All(d.Stavke, s => Assert.False(s.DodaoKupac));
        // Ponuda je cijela sezonska ponuda (odluka P2b), ne samo kategorije ove korpice.
        Assert.Contains(d.Ponuda, p => p.Naziv == "Sir" && !p.IzOveKorpice);
        Assert.Contains(d.Ponuda, p => p.Naziv == "Krompir" && p.IzOveKorpice);
    }

    [Fact]
    public async Task Detalji_tudje_korpice_daju_403()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var orderItemId = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<EPiljara.Domain.Common.ForbiddenException>(
            () => t.MojaKorpicaAsync(orderItemId, kao: Guid.NewGuid()));
    }

    [Fact]
    public async Task Detalji_obicne_stavke_svoje_narudzbe_ne_postoje()
    {
        // Nalaz F2: detaljni GET je na id običnog reda (1 kg krompira) vraćao cijelu podobnu
        // ponudu i time davao sastojke za upis kroz PUT. Obična stavka narudžbe nije korpica,
        // pa za ovaj endpoint nije ni resurs.
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiObicanArtikalAsync();
        var orderItemId = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<EPiljara.Domain.Common.NotFoundException>(
            () => t.MojaKorpicaAsync(orderItemId));
    }
}
