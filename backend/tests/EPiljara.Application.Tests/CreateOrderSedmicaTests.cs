using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class CreateOrderSedmicaTests
{
    private static async Task<(TestDb, Guid zonaId, Guid slotId, Guid productId)> PripremiAsync()
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

    private static CreateOrderHandler Handler(TestDb t) =>
        new(t.Db, new FakeCurrentUser(null), new FakeEmail(), new FakeViber(),
            Options.Create(TestOpcije.Korpice()), Options.Create(TestOpcije.Pretplate()), Options.Create(TestOpcije.Poeni()));

    [Fact]
    public async Task Narudzba_dobija_ISO_sedmicu_isporuke()
    {
        var (t, zonaId, slotId, productId) = await PripremiAsync();
        using var _ = t;

        var cmd = new CreateOrderCommand(
            new CustomerInput("Marko", "marko@example.com", "065"),
            zonaId, slotId, "Testna 1", null,
            [new OrderItemInput(productId, 1m)]);

        var dto = await Handler(t).Handle(cmd, default);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);

        var (dan, vrijeme, cutoff) = await SedmicaHelper.UcitajRokAsync(
            t.Db, TestOpcije.Korpice(), default);
        var (ocekivanaGodina, ocekivanaSedmica) =
            SedmicaHelper.SedmicaZaNarudzbu(dan, vrijeme, cutoff);

        Assert.Equal(ocekivanaGodina, order.Godina);
        Assert.Equal(ocekivanaSedmica, order.Sedmica);
        // Ne smije ostati na podrazumijevanoj nuli — to je cio smisao ovog testa.
        Assert.NotEqual(0, order.Sedmica);
    }
}
