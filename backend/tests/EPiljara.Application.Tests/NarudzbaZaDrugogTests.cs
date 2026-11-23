using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Orders;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba za nekog drugog: Customer* ostaje naručilac (potvrda ide njemu), Primalac* je
/// osoba kojoj se nosi — nju vide vozač i lista pakovanja.
/// </summary>
public class NarudzbaZaDrugogTests
{
    private static async Task<(TestDb t, Guid zonaId, Guid slotId, Guid productId)> PripremiAsync()
    {
        var t = new TestDb();
        var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true, Granica = TestNaselje.Granica };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var p = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg, Stock = 100, IsAvailable = true,
            Category = new Category { Name = "Povrće", Slug = "povrce" },
            Producer = new Producer { Name = "Marko", Village = "Manjača" },
        };
        t.Db.DeliveryZones.Add(zona);
        t.Db.DeliverySlots.Add(slot);
        t.Db.Products.Add(p);
        await t.Db.SaveChangesAsync();
        return (t, zona.Id, slot.Id, p.Id);
    }

    private static CreateOrderHandler Handler(TestDb t) =>
        new(t.Db, new FakeCurrentUser(Guid.NewGuid()), new FakeEmail(), new FakeViber(),
            Options.Create(TestOpcije.Korpice()), Options.Create(TestOpcije.Pretplate()), Options.Create(TestOpcije.Poeni()), TestOpcije.DostavaO());

    private static CreateOrderCommand Komanda(Guid zonaId, Guid slotId, Guid productId, PrimalacInput? primalac) =>
        new(new CustomerInput("Marko", "marko@example.com", "065"),
            zonaId, slotId, "Kozaračka 5", null,
            [new OrderItemInput(productId, 1m)],
            Latitude: TestNaselje.Lat, Longitude: TestNaselje.Lon, Primalac: primalac);

    private static readonly PrimalacInput Petar = new("Petar Petrović", "+387 65 111 222");

    [Fact]
    public async Task Narudzba_snima_primaoca_a_kupac_ostaje_narucilac()
    {
        var (t, z, s, p) = await PripremiAsync();
        using var _ = t;

        var dto = await Handler(t).Handle(Komanda(z, s, p, Petar), default);

        dto.PrimalacIme.Should().Be("Petar Petrović");
        dto.PrimalacTelefon.Should().Be("+387 65 111 222");
        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);
        order.CustomerName.Should().Be("Marko");
        order.CustomerEmail.Should().Be("marko@example.com");
        order.PrimalacIme.Should().Be("Petar Petrović");
        order.PrimalacTelefon.Should().Be("+387 65 111 222");
    }

    [Fact]
    public void Stanica_rute_nosi_primaoca()
    {
        var order = new Order { CustomerName = "Marko", CustomerPhone = "065", PrimalacIme = "Petar Petrović", PrimalacTelefon = "+387 65 111 222" };

        var stop = new RouteStop { Order = order }.ToDto();

        stop.CustomerName.Should().Be("Petar Petrović");
        stop.CustomerPhone.Should().Be("+387 65 111 222");
    }

    [Fact]
    public void Stanica_bez_primaoca_nosi_kupca()
    {
        var stop = new RouteStop { Order = new Order { CustomerName = "Marko", CustomerPhone = "065" } }.ToDto();

        stop.CustomerName.Should().Be("Marko");
        stop.CustomerPhone.Should().Be("065");
    }

    [Fact]
    public void Primalac_mora_imati_i_telefon()
    {
        var r = new CreateOrderValidator().Validate(
            Komanda(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new PrimalacInput("Petar", "")));

        r.Errors.Should().Contain(e => e.PropertyName == "Primalac.Telefon");
    }

    [Fact]
    public async Task Lista_pakovanja_pokazuje_primaoca()
    {
        var (t, z, s, p) = await PripremiAsync();
        using var _ = t;
        var dto = await Handler(t).Handle(Komanda(z, s, p, Petar), default);
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);

        var pak = await new GetPakovanjeHandler(t.Db).Handle(new GetPakovanjeQuery(order.Godina, order.Sedmica), default);

        var red = pak.Narudzbe.Single(n => n.Id == dto.Id);
        red.Kupac.Should().Be("Petar Petrović");
        red.Telefon.Should().Be("+387 65 111 222");
    }
}
