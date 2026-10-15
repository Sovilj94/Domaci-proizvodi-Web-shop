using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Poeni;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>Knjiga poena (P6c, Task 1): pravila trošenja i zarade te upis pri promjeni statusa.</summary>
public class PoeniPravilaTests
{
    private static readonly PoeniOpcije O = new();

    [Theory]
    [InlineData(0, 50, 0, 0)]
    [InlineData(-40, 50, 0, 0)]
    [InlineData(30, 50, 20, 1)]
    [InlineData(1000, 50, 1000, 50)]
    [InlineData(19, 50, 0, 0)]
    public void ZaTrosenje_zaokruzuje_na_cijele_KM(int stanje, double roba, int poena, int popust)
    {
        var (p, d) = PoeniPravila.ZaTrosenje(stanje, (decimal)roba, O);
        Assert.Equal(poena, p);
        Assert.Equal(popust, d);
    }

    [Fact]
    public void ZaTrosenje_ogranicen_robom()
    {
        var (p, d) = PoeniPravila.ZaTrosenje(1000, 12.70m, O);
        Assert.Equal(240, p);
        Assert.Equal(12m, d);
    }

    [Fact]
    public void Zarada_racuna_robu_poslije_popusta_bez_dostave()
    {
        var o = new Order { Subtotal = 33.90m, Popust = 5m, PoeniPopust = 2m, DeliveryFee = 4m };
        Assert.Equal(26, PoeniPravila.ZaradaZaNarudzbu(o, O));
        Assert.Equal(0, PoeniPravila.ZaradaZaNarudzbu(new Order { Subtotal = 0.99m }, O));
    }

    private static async Task<(TestDb t, User kupac, Order order)> PripremiAsync(
        OrderStatus status = OrderStatus.OutForDelivery, bool gost = false, decimal subtotal = 30m)
    {
        var t = new TestDb();
        var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var kupac = new User { Name = "Mujo", Email = $"{Guid.NewGuid():N}@x.ba" };
        t.Db.AddRange(zona, slot, kupac);
        await t.Db.SaveChangesAsync();
        var order = new Order
        {
            Code = "EP-0001", PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = gost ? null : kupac.Id, CustomerName = "Mujo", Status = status,
            DeliveryAddress = "Testna 1", DeliverySlotId = slot.Id, DeliveryZoneId = zona.Id,
            Subtotal = subtotal,
        };
        t.Db.Orders.Add(order);
        await t.Db.SaveChangesAsync();
        return (t, kupac, order);
    }

    private static async Task Pozovi(TestDb t, Order o, OrderStatus novi)
    {
        await PoeniPravila.ZabiljeziPromjenuStatusaAsync(t.Db, o, novi, O, default);
        await t.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Isporuka_upise_zaradu_jednom()
    {
        var (t, kupac, order) = await PripremiAsync();
        using var _ = t;

        await Pozovi(t, order, OrderStatus.Delivered);
        var s = await t.Db.PoeniStavke.SingleAsync();
        Assert.Equal((VrstaPoena.Isporuka, 30, kupac.Id, order.Id), (s.Vrsta, s.Iznos, s.UserId, s.OrderId!.Value));

        // Već isporučena: nema promjene statusa.
        order.Status = OrderStatus.Delivered;
        await Pozovi(t, order, OrderStatus.Delivered);
        // Ponovni zahtjev dok je status opet OutForDelivery: stavka već postoji.
        order.Status = OrderStatus.OutForDelivery;
        await Pozovi(t, order, OrderStatus.Delivered);
        Assert.Equal(1, await t.Db.PoeniStavke.CountAsync());
    }

    [Fact]
    public async Task Gost_ne_dobija_poene()
    {
        var (t, _, order) = await PripremiAsync(gost: true);
        using var _t = t;
        await Pozovi(t, order, OrderStatus.Delivered);
        Assert.Empty(t.Db.PoeniStavke);
    }

    [Fact]
    public async Task Nulta_zarada_nema_stavke()
    {
        var (t, _, order) = await PripremiAsync(subtotal: 0.99m);
        using var _t = t;
        await Pozovi(t, order, OrderStatus.Delivered);
        Assert.Empty(t.Db.PoeniStavke);
    }

    [Fact]
    public async Task Preporuka_nagradjuje_obje_strane_samo_za_prvu_isporuku()
    {
        var (t, kupac, order) = await PripremiAsync();
        using var _t = t;
        var a = new User { Name = "A", Email = "a@x.ba" };
        t.Db.Users.Add(a);
        await t.Db.SaveChangesAsync();
        kupac.PreporucioId = a.Id;
        await t.Db.SaveChangesAsync();

        await Pozovi(t, order, OrderStatus.Delivered);
        var novi = await t.Db.PoeniStavke.SingleAsync(s => s.Vrsta == VrstaPoena.PreporukaNovi);
        var pre = await t.Db.PoeniStavke.SingleAsync(s => s.Vrsta == VrstaPoena.PreporukaPreporucio);
        Assert.Equal((kupac.Id, 100), (novi.UserId, novi.Iznos));
        Assert.Equal((a.Id, 100, (Guid?)kupac.Id), (pre.UserId, pre.Iznos, pre.PreporuceniId));

        var druga = new Order
        {
            Code = "EP-0002", PublicToken = Guid.NewGuid().ToString("N"), CustomerId = kupac.Id,
            CustomerName = "Mujo", Status = OrderStatus.OutForDelivery, DeliveryAddress = "T",
            DeliverySlotId = order.DeliverySlotId, DeliveryZoneId = order.DeliveryZoneId, Subtotal = 10m,
        };
        t.Db.Orders.Add(druga);
        await t.Db.SaveChangesAsync();
        await Pozovi(t, druga, OrderStatus.Delivered);

        Assert.Equal(1, await t.Db.PoeniStavke.CountAsync(s => s.Vrsta == VrstaPoena.PreporukaNovi));
        Assert.Equal(1, await t.Db.PoeniStavke.CountAsync(s => s.Vrsta == VrstaPoena.PreporukaPreporucio));
    }

    [Fact]
    public async Task Obrisan_preporucilac_ne_nagradjuje_nikoga()
    {
        var (t, kupac, order) = await PripremiAsync(subtotal: 0.5m);
        using var _t = t;
        // Dangling referenca se ne može napraviti uz FK, pa ga na trenutak gasimo.
        await t.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        await t.Db.Database.ExecuteSqlRawAsync(
            "UPDATE Users SET PreporucioId = {0} WHERE Id = {1}", Guid.NewGuid(), kupac.Id);
        await t.Db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        await Pozovi(t, order, OrderStatus.Delivered);
        Assert.Empty(t.Db.PoeniStavke);

        // Kontrola (zarada je i dalje 0, pa jedino provjera postojanja preporučioca odlučuje):
        // isti kupac sa preporučiocem koji postoji dobija obje nagrade. Bez ove kontrole prazna
        // knjiga gore ne bi dokazivala da je upravo Users.AnyAsync zaustavio upis.
        var a = new User { Name = "A", Email = "a@x.ba" };
        t.Db.Users.Add(a);
        await t.Db.SaveChangesAsync();
        await t.Db.Database.ExecuteSqlRawAsync(
            "UPDATE Users SET PreporucioId = {0} WHERE Id = {1}", a.Id, kupac.Id);
        t.Db.ChangeTracker.Clear();
        var svjez = await t.Db.Orders.FirstAsync(o => o.Id == order.Id);
        await Pozovi(t, svjez, OrderStatus.Delivered);
        Assert.Equal(2, await t.Db.PoeniStavke.CountAsync());
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.FailedDelivery)]
    public async Task Otkaz_vraca_potrosene_poene_jednom(OrderStatus stari)
    {
        var (t, kupac, order) = await PripremiAsync(stari);
        using var _t = t;
        order.IskoristeniPoeni = 40;

        await Pozovi(t, order, OrderStatus.Cancelled);
        var s = await t.Db.PoeniStavke.SingleAsync();
        Assert.Equal((VrstaPoena.Povrat, 40, kupac.Id, order.Id), (s.Vrsta, s.Iznos, s.UserId, s.OrderId!.Value));

        order.Status = stari;
        await Pozovi(t, order, OrderStatus.Cancelled);
        Assert.Equal(1, await t.Db.PoeniStavke.CountAsync());
    }

    [Fact]
    public async Task Otkaz_bez_potrosenih_poena_nema_stavke()
    {
        var (t, _, order) = await PripremiAsync(OrderStatus.Pending);
        using var _t = t;
        await Pozovi(t, order, OrderStatus.Cancelled);
        Assert.Empty(t.Db.PoeniStavke);
    }

    [Fact]
    public async Task Stanje_je_zbir_stavki()
    {
        var (t, kupac, _) = await PripremiAsync();
        using var _t = t;
        Assert.Equal(0, await PoeniPravila.StanjeAsync(t.Db, kupac.Id, default));
        foreach (var (v, i) in new[] { (VrstaPoena.PreporukaNovi, 100), (VrstaPoena.Iskoristeno, -40), (VrstaPoena.Povrat, 40) })
            t.Db.PoeniStavke.Add(new PoeniStavka { UserId = kupac.Id, Iznos = i, Vrsta = v });
        await t.Db.SaveChangesAsync();
        Assert.Equal(100, await PoeniPravila.StanjeAsync(t.Db, kupac.Id, default));
    }

    [Fact]
    public async Task Isporucena_narudzba_admina_ne_pise_poene()
    {
        var (t, kupac, order) = await PripremiAsync();
        using var _t = t;
        kupac.Role = UserRole.Admin;
        await t.Db.SaveChangesAsync();
        await Pozovi(t, order, OrderStatus.Delivered);
        Assert.Empty(t.Db.PoeniStavke);
    }

    [Fact]
    public async Task Handler_za_Delivered_upise_isporuku()
    {
        var (t, _, order) = await PripremiAsync();
        using var _t = t;
        await new UpdateOrderStatusHandler(t.Db, Options.Create(O))
            .Handle(new UpdateOrderStatusCommand(order.Id, "Delivered"), default);
        var s = await t.Db.PoeniStavke.SingleAsync();
        Assert.Equal((VrstaPoena.Isporuka, 30), (s.Vrsta, s.Iznos));
    }
}
