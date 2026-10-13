using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class CreateOrderKorpicaTests
{
    [Fact]
    public async Task Korpica_dobija_snapshot_sadrzaja_te_sedmice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);

        t.Db.ChangeTracker.Clear();
        var stavke = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .Where(s => s.OrderItem!.OrderId == dto.Id)
            .ToListAsync();

        Assert.Equal(2, stavke.Count);
        Assert.All(stavke, s => Assert.False(s.DodaoKupac));
        Assert.Contains(stavke, s => s.ProductName == "Krompir" && s.Kolicina == 2m);
        Assert.Contains(stavke, s => s.ProductName == "Paprika" && s.Kolicina == 1m);
    }

    [Fact]
    public async Task Korpica_bez_objavljenog_sadrzaja_se_ne_moze_naruciti()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.PosaljiKorpicuAsync(kolicina: 1m));

        Assert.Equal("korpica_bez_sadrzaja", ex.Code);
    }

    [Fact]
    public async Task Sadrzaj_se_cuva_jednom_bez_obzira_na_kolicinu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiKorpicuAsync(kolicina: 3m);

        t.Db.ChangeTracker.Clear();
        var stavke = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .Where(s => s.OrderItem!.OrderId == dto.Id)
            .ToListAsync();

        // Tri kutije, ali sadržaj je i dalje dva reda. Množenje je posao pakerske liste —
        // provjeri i da Kolicina nije tiho potrojena umjesto redova.
        Assert.Equal(2, stavke.Count);
        Assert.Contains(stavke, s => s.ProductName == "Krompir" && s.Kolicina == 2m);
        Assert.Contains(stavke, s => s.ProductName == "Paprika" && s.Kolicina == 1m);
    }

    [Fact]
    public async Task Soft_obrisan_artikal_u_sadrzaju_i_dalje_dobija_pravi_naziv_i_jedinicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        // Krompir je Unit.Kg, ne Unit.Kom — bitno da assert na jedinici nešto dokazuje.
        var krompir = await t.Db.Products.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == t.KrompirId);
        krompir.IsDeleted = true;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);

        t.Db.ChangeTracker.Clear();
        var stavka = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .FirstAsync(s => s.OrderItem!.OrderId == dto.Id && s.ProductId == t.KrompirId);

        Assert.Equal("Krompir", stavka.ProductName);
        Assert.Equal(Unit.Kg, stavka.Unit);
    }

    [Fact]
    public async Task Obican_artikal_ne_dobija_nijednu_stavku_korpice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiObicanArtikalAsync();

        t.Db.ChangeTracker.Clear();
        var broj = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .CountAsync(s => s.OrderItem!.OrderId == dto.Id);

        Assert.Equal(0, broj);
    }

    // ---------- Druga vrata u Orders: potvrda zahtjeva gosta (nalaz F1) ----------

    [Fact]
    public async Task Gostova_korpica_dobija_sedmicu_i_snapshot_sadrzaja()
    {
        // prijavljen: false — gost. Zahtjev nosi samo namjeru; sedmica i sadržaj nastaju na
        // potvrdu, jer tada narudžba stvarno nastaje.
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var token = await t.ZahtjevGostaAsync(t.KorpicaId);

        var dto = await t.PotvrdiGostaAsync(token);

        t.Db.ChangeTracker.Clear();
        var narudzba = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        Assert.Equal(t.Godina, narudzba.Godina);
        Assert.Equal(t.Sedmica, narudzba.Sedmica);

        var stavke = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .Where(s => s.OrderItem!.OrderId == dto.Id)
            .ToListAsync();

        Assert.Equal(2, stavke.Count);
        Assert.All(stavke, s => Assert.False(s.DodaoKupac));
        Assert.Contains(stavke, s => s.ProductName == "Krompir" && s.Kolicina == 2m);
        Assert.Contains(stavke, s => s.ProductName == "Paprika" && s.Kolicina == 1m);
    }

    [Fact]
    public async Task Gost_ne_moze_potvrditi_korpicu_bez_objavljenog_sadrzaja()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: false);
        var token = await t.ZahtjevGostaAsync(t.KorpicaId);

        var ex = await Assert.ThrowsAsync<DomainException>(() => t.PotvrdiGostaAsync(token));

        Assert.Equal("korpica_bez_sadrzaja", ex.Code);
        t.Db.ChangeTracker.Clear();
        Assert.Empty(await t.Db.Orders.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Gostova_narudzba_obicnog_artikla_i_dalje_dobija_sedmicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var token = await t.ZahtjevGostaAsync(t.KrompirId);

        var dto = await t.PotvrdiGostaAsync(token);

        // Sedmica je invarijanta SVAKE narudžbe, ne samo one sa korpicom — na njoj stoji
        // odgovor na „koliko je kutija naručeno ove sedmice". 0/0 je bilo nerazlučivo od
        // starog, nepopunjenog reda iz migracije.
        t.Db.ChangeTracker.Clear();
        var narudzba = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        Assert.NotEqual(0, narudzba.Sedmica);
        Assert.Equal(t.Godina, narudzba.Godina);
        Assert.Equal(t.Sedmica, narudzba.Sedmica);
    }
}
