using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba koju admin nije potvrdio do roka naručivanja automatski se odbija: roba za tu
/// sedmicu se već nabavlja samo po potvrđenim, pa nepotvrđena ne može biti isporučena.
/// </summary>
public class OdbijanjeNepotvrdjenihTests
{
    // Petak 41/2026 u 16:00, rok 48h ranije = srijeda 41/2026 u 16:00 (po Sarajevu).
    private static readonly DateTime PoslijeRoka = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Friday);
    private static readonly DateTime PrijeRoka = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Monday);

    private sealed record Osnova(TestDb T, Guid ZonaId, Guid SlotId, Product Artikal, User Kupac);

    private static Osnova Napravi()
    {
        var t = new TestDb();
        var slot = new DeliverySlot { DayOfWeek = 5, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var kat = new Category { Name = "Mliječno", Slug = "mlijecno" };
        var proiz = new Producer { Name = "Jelena Petrović", Village = "Šargovac" };
        var sir = new Product
        {
            Name = "Sir", Slug = "sir", Price = 10m, Unit = Unit.Kg, Stock = 5m,
            CategoryId = kat.Id, ProducerId = proiz.Id, Vrsta = VrstaProizvoda.Obican, USpajzu = true,
        };
        var kupac = new User { Name = "Marko Marković", Email = "marko@example.com", Role = UserRole.Customer };
        t.Db.AddRange(slot, zona, kat, proiz, sir, kupac);
        t.Db.SaveChanges();
        return new Osnova(t, zona.Id, slot.Id, sir, kupac);
    }

    private static Order Narudzba(Osnova o, OrderStatus status, int poeni = 0)
    {
        var n = new Order
        {
            Code = $"EP-{Guid.NewGuid():N}"[..12], PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = o.Kupac.Id, CustomerName = o.Kupac.Name, Status = status,
            DeliveryZoneId = o.ZonaId, DeliverySlotId = o.SlotId, Godina = 2026, Sedmica = 41,
            IskoristeniPoeni = poeni,
        };
        n.Items.Add(new OrderItem
        {
            OrderId = n.Id, ProductId = o.Artikal.Id, ProductName = o.Artikal.Name, Unit = Unit.Kg,
            Quantity = 2m, UnitPrice = 10m,
        });
        o.T.Db.Orders.Add(n);
        o.T.Db.SaveChanges();
        return n;
    }

    private static Task<int> OdbijAsync(Osnova o, DateTime sada) =>
        new OdbijNepotvrdjeneHandler(o.T.Db, Options.Create(TestOpcije.Korpice()), Options.Create(new Features.Poeni.PoeniOpcije()), new FakeEmail())
            .Handle(new OdbijNepotvrdjeneCommand(sada), default);

    [Fact]
    public async Task Narudzba_na_cekanju_poslije_roka_se_odbija_i_vraca_robu_na_stanje()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Pending);

        Assert.Equal(1, await OdbijAsync(o, PoslijeRoka));

        o.T.Db.ChangeTracker.Clear();
        Assert.Equal(OrderStatus.Cancelled, (await o.T.Db.Orders.SingleAsync(x => x.Id == n.Id)).Status);
        Assert.Equal(7m, (await o.T.Db.Products.SingleAsync(x => x.Id == o.Artikal.Id)).Stock);
    }

    [Fact]
    public async Task Prije_roka_narudzba_ostaje_na_cekanju()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Pending);

        Assert.Equal(0, await OdbijAsync(o, PrijeRoka));

        o.T.Db.ChangeTracker.Clear();
        Assert.Equal(OrderStatus.Pending, (await o.T.Db.Orders.SingleAsync(x => x.Id == n.Id)).Status);
        Assert.Equal(5m, (await o.T.Db.Products.SingleAsync(x => x.Id == o.Artikal.Id)).Stock);
    }

    [Fact]
    public async Task Potvrdjena_narudzba_se_ne_dira()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Confirmed);

        Assert.Equal(0, await OdbijAsync(o, PoslijeRoka));

        o.T.Db.ChangeTracker.Clear();
        Assert.Equal(OrderStatus.Confirmed, (await o.T.Db.Orders.SingleAsync(x => x.Id == n.Id)).Status);
    }

    [Fact]
    public async Task Odbijena_narudzba_vraca_potrosene_poene()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Pending, poeni: 40);

        await OdbijAsync(o, PoslijeRoka);
        await OdbijAsync(o, PoslijeRoka.AddHours(1)); // drugi prolaz ne vraća još jednom

        var povrat = await o.T.Db.PoeniStavke.Where(s => s.OrderId == n.Id && s.Vrsta == VrstaPoena.Povrat).ToListAsync();
        Assert.Equal(40, Assert.Single(povrat).Iznos);
    }
}
