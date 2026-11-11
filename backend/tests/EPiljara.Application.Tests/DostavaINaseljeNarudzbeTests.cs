using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Naselje određuje server iz tačke; dostava je besplatna uz korpicu, inače 5 KM.</summary>
public class DostavaINaseljeNarudzbeTests
{
    [Fact]
    public async Task Narudzba_dobija_naselje_iz_koordinata_a_ne_iz_zahtjeva()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);
        var daleko = new DeliveryZone { Name = "Bijeljina", IsActive = true, Granica = TestNaselje.GranicaDaleko };
        s.Db.DeliveryZones.Add(daleko);
        await s.Db.SaveChangesAsync();

        var dto = await s.PosaljiAsync(s.KrompirId, zonaId: daleko.Id);

        var narudzba = await s.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == dto.Id);
        Assert.Equal(s.ZonaId, narudzba.DeliveryZoneId);
    }

    [Fact]
    public async Task Narudzba_van_naselja_se_odbija()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => s.PosaljiAsync(s.KrompirId, lat: TestNaselje.VanLat, lon: TestNaselje.VanLon));

        Assert.Equal("van_podrucja_dostave", ex.Code);
        Assert.False(await s.Db.Orders.AnyAsync());
    }

    [Fact]
    public void Narudzba_bez_koordinata_se_odbija()
    {
        var cmd = new CreateOrderCommand(
            new CustomerInput("Ana", "ana@example.com", "065 111 222"),
            null, Guid.NewGuid(), "Reljina 18", null, [new OrderItemInput(Guid.NewGuid(), 1m)]);

        var r = new CreateOrderValidator().Validate(cmd);

        Assert.Contains(r.Errors, e => e.PropertyName == "deliveryAddress" && e.ErrorMessage == "Označi adresu na mapi.");
    }

    [Fact]
    public void Pretplatna_komanda_bez_koordinata_prolazi_validaciju()
    {
        var cmd = new CreateOrderCommand(
            new CustomerInput("Ana", "ana@example.com", "065 111 222"),
            Guid.NewGuid(), Guid.NewGuid(), "Reljina 18", null, [new OrderItemInput(Guid.NewGuid(), 1m)],
            PretplataId: Guid.NewGuid());

        var r = new CreateOrderValidator().Validate(cmd);

        Assert.DoesNotContain(r.Errors, e => e.ErrorMessage == "Označi adresu na mapi.");
    }

    [Fact]
    public async Task Korpica_besplatna_dostava()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await s.PosaljiKorpicuAsync(kolicina: 1m);

        var narudzba = await s.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == dto.Id);
        Assert.Equal(0m, narudzba.DeliveryFee);
        Assert.Equal(narudzba.Subtotal, narudzba.TotalAmount);
    }

    [Fact]
    public async Task Bez_korpice_dostava_5()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var dto = await s.PosaljiObicanArtikalAsync();

        var narudzba = await s.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == dto.Id);
        Assert.Equal(5m, narudzba.DeliveryFee);
        Assert.Equal(narudzba.Subtotal + 5m, narudzba.TotalAmount);
    }

    [Fact]
    public async Task Minimum_iz_podesavanja()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);
        s.Dostava = TestOpcije.Dostava(minimalna: 20m);

        // Krompir je 2 KM.
        var ex = await Assert.ThrowsAsync<DomainException>(() => s.PosaljiObicanArtikalAsync());

        Assert.Equal("below_minimum", ex.Code);
        Assert.Equal("Minimalna narudžba je 20 KM.", ex.Message);
    }

    [Fact]
    public async Task Gost_zahtjev_korpica_besplatno_a_bez_nje_5()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: true);

        await s.ZahtjevGostaAsync(s.KorpicaId);
        await s.ZahtjevGostaAsync(s.KrompirId);

        var cijene = await s.Db.GuestOrderRequests.AsNoTracking()
            .OrderBy(r => r.CreatedAt).Select(r => r.DeliveryFee).ToListAsync();
        Assert.Equal([0m, 5m], cijene);
    }

    [Fact]
    public async Task Gost_van_naselja_se_odbija()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => s.ZahtjevGostaAsync(s.KrompirId, lat: TestNaselje.VanLat, lon: TestNaselje.VanLon));

        Assert.Equal("van_podrucja_dostave", ex.Code);
    }

    [Fact]
    public async Task Gost_potvrda_ponovo_provjerava_naselje()
    {
        using var s = await Scenario.NapraviAsync(objaviSadrzaj: false);
        var token = await s.ZahtjevGostaAsync(s.KrompirId);

        // Admin je u međuvremenu pomjerio granicu tako da adresa više nije u naselju.
        var zona = await s.Db.DeliveryZones.SingleAsync(z => z.Id == s.ZonaId);
        zona.Granica = TestNaselje.GranicaDaleko;
        await s.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.PotvrdiGostaAsync(token));

        Assert.Equal("van_podrucja_dostave", ex.Code);
        Assert.False(await s.Db.Orders.AnyAsync());
    }
}
