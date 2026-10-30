using EPiljara.Application.Features.Poeni;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EPiljara.Application.Tests;

public class OrderTrackingTests
{
    [Fact]
    public async Task Nova_narudzba_dobija_javni_token()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        var rezultat = await scena.Naruci();

        var order = t.Db.Orders.Single(o => o.Id == rezultat.Id);
        order.PublicToken.Should().NotBeNullOrWhiteSpace();
        order.PublicToken.Should().HaveLength(32, "token je Guid bez crtica");
    }

    [Fact]
    public async Task Dvije_narudzbe_ne_dijele_token()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        var prva = await scena.Naruci();
        var druga = await scena.Naruci();

        var tokeni = t.Db.Orders
            .Where(o => o.Id == prva.Id || o.Id == druga.Id)
            .Select(o => o.PublicToken)
            .ToList();
        tokeni.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Pracenje_po_tokenu_vraca_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var narucena = await scena.Naruci();
        var token = t.Db.Orders.Single(o => o.Id == narucena.Id).PublicToken;

        var rezultat = await new TrackOrderHandler(t.Db).Handle(new TrackOrderQuery(token), default);

        rezultat.Id.Should().Be(narucena.Id);
        rezultat.CustomerEmail.Should().Be("marko@test.local");
        rezultat.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Pracenje_sa_pogresnim_tokenom_baca_not_found()
    {
        using var t = new TestDb();

        var radnja = () => new TrackOrderHandler(t.Db)
            .Handle(new TrackOrderQuery("nepostojeci"), default);

        await radnja.Should().ThrowAsync<NotFoundException>();
    }
}

internal sealed class Scena
{
    private readonly TestDb _t;
    private readonly Guid _zoneId;
    private readonly Guid _slotId;
    private readonly Guid _productId;

    private Scena(TestDb t, Guid zoneId, Guid slotId, Guid productId)
    {
        _t = t; _zoneId = zoneId; _slotId = slotId; _productId = productId;
    }

    public static Task<Scena> NapraviAsync(TestDb t, decimal minimalniIznos = 10m)
    {
        // Product ima obavezne CategoryId i ProducerId, a TestDb ima uključene
        // strane ključeve — bez ova dva reda upis proizvoda puca.
        var category = new Category { Name = "Med i zimnica", Slug = "med-i-zimnica" };
        var producer = new Producer { Name = "Porodica Kovačević", Village = "Han Kola" };
        var zone = new DeliveryZone { Name = "Centar", MinOrderAmount = minimalniIznos, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", Label = "Četvrtak 16–20h" };
        t.Db.Categories.Add(category);
        t.Db.Producers.Add(producer);
        t.Db.DeliveryZones.Add(zone);
        t.Db.DeliverySlots.Add(slot);
        t.Db.SaveChanges();

        var product = new Product
        {
            Name = "Med", Slug = "med", Price = 20m, Stock = 100,
            IsAvailable = true, Unit = Domain.Enums.Unit.Kom,
            CategoryId = category.Id, ProducerId = producer.Id,
        };
        t.Db.Products.Add(product);
        t.Db.SaveChanges();
        return Task.FromResult(new Scena(t, zone.Id, slot.Id, product.Id));
    }

    public Task<OrderDto> Naruci() =>
        new CreateOrderHandler(
                _t.Db, new FakeCurrentUser(null), new FakeEmail(), new FakeViber(),
                Options.Create(TestOpcije.Korpice()), Options.Create(TestOpcije.Pretplate()), Options.Create(TestOpcije.Poeni()))
            .Handle(Komanda(), default);

    public CreateOrderCommand Komanda() => new(
        new CustomerInput("Marko Marković", "marko@test.local", "065 111 222"),
        _zoneId, _slotId, "Kralja Petra 12", null,
        [new OrderItemInput(_productId, 1)], 44.77, 17.19);

    public CreateGuestOrderRequestCommand ZahtjevGosta(string? napomena = null) => new(
        new CustomerInput("Marko Marković", "marko@test.local", "065 111 222"),
        _zoneId, _slotId, "Kralja Petra 12", napomena,
        [new OrderItemInput(_productId, 1)], 44.77, 17.19);
}
