using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// `CreateOrderCommand.CustomerId` postoji zbog jednog pozivaoca: posla koji pravi
/// pretplatne narudžbe, a koji nema prijavljenog korisnika. Ovi testovi drže oba smjera —
/// da posao može da upiše vlasnika, i da se za zahtjeve sa žice ništa nije promijenilo.
/// </summary>
public class CreateOrderVlasnikTests
{
    private static async Task<(TestDb t, Guid zonaId, Guid slotId, Guid productId)> PripremiAsync()
    {
        var t = new TestDb();

        var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proizvodjac = new Producer { Name = "Marko", Village = "Manjača" };
        var p = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg,
            Stock = 100, IsAvailable = true, Category = kat, Producer = proizvodjac,
        };

        t.Db.DeliveryZones.Add(zona);
        t.Db.DeliverySlots.Add(slot);
        t.Db.Products.Add(p);
        await t.Db.SaveChangesAsync();

        return (t, zona.Id, slot.Id, p.Id);
    }

    private static CreateOrderHandler Handler(TestDb t, Guid? prijavljen) =>
        new(t.Db, new FakeCurrentUser(prijavljen), new FakeEmail(), new FakeViber(),
            Options.Create(new KorpiceOpcije()), Options.Create(new PretplateOpcije()), Options.Create(new EPiljara.Application.Features.Poeni.PoeniOpcije()));

    private static CreateOrderCommand Komanda(
        Guid zonaId, Guid slotId, Guid productId,
        Guid? customerId = null, Guid? pretplataId = null) =>
        new(new CustomerInput("Mujo", "mujo@example.com", "065"),
            zonaId, slotId, "Testna 1", null,
            [new OrderItemInput(productId, 1m)],
            CustomerId: customerId, PretplataId: pretplataId);

    [Fact]
    public async Task Posao_bez_prijavljenog_korisnika_upisuje_vlasnika_iz_komande()
    {
        var (t, zonaId, slotId, productId) = await PripremiAsync();
        using var _ = t;
        var kupac = Guid.NewGuid();
        var pretplata = Guid.NewGuid();

        var dto = await Handler(t, prijavljen: null)
            .Handle(Komanda(zonaId, slotId, productId, kupac, pretplata), default);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        Assert.Equal(kupac, order.CustomerId);
        Assert.Equal(pretplata, order.PretplataId);
    }

    [Fact]
    public async Task Bez_CustomerId_prijavljeni_kupac_i_dalje_dobija_svoju_narudzbu()
    {
        var (t, zonaId, slotId, productId) = await PripremiAsync();
        using var _ = t;
        var prijavljen = Guid.NewGuid();

        var dto = await Handler(t, prijavljen).Handle(Komanda(zonaId, slotId, productId), default);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        Assert.Equal(prijavljen, order.CustomerId);
        Assert.Null(order.PretplataId);
    }

    [Fact]
    public async Task Bez_CustomerId_gost_i_dalje_ostaje_bez_vlasnika()
    {
        var (t, zonaId, slotId, productId) = await PripremiAsync();
        using var _ = t;

        var dto = await Handler(t, prijavljen: null)
            .Handle(Komanda(zonaId, slotId, productId), default);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        Assert.Null(order.CustomerId);
    }
}
