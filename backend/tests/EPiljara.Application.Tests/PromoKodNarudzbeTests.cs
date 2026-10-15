using System.Text.Json;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

public class PromoKodNarudzbeTests
{
    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public DeliveryZone Zona { get; set; } = null!;
        public DeliverySlot Slot { get; set; } = null!;
        public Product Proizvod { get; set; } = null!;

        public static async Task<Okruzenje> NapraviAsync(decimal minimum = 0m, decimal cijena = 20m)
        {
            var o = new Okruzenje();
            o.Zona = new DeliveryZone { Name = "Centar", MinOrderAmount = minimum, DeliveryFee = 3m, IsActive = true };
            o.Slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            o.Proizvod = new Product
            {
                Name = "Med", Slug = "med", Price = cijena, Unit = Unit.Kom, Stock = 100, IsAvailable = true,
                Category = new Category { Name = "Med", Slug = "med" },
                Producer = new Producer { Name = "Marko", Village = "Manjača" },
            };
            o.T.Db.DeliveryZones.Add(o.Zona);
            o.T.Db.DeliverySlots.Add(o.Slot);
            o.T.Db.Products.Add(o.Proizvod);
            await o.T.Db.SaveChangesAsync();
            return o;
        }

        public async Task<PromoKod> KodAsync(Action<PromoKod>? podesi = null)
        {
            var k = new PromoKod { Kod = "JESEN5", Opis = "Test", Tip = TipPopusta.Iznos, Vrijednost = 5m };
            podesi?.Invoke(k);
            T.Db.PromoKodovi.Add(k);
            await T.Db.SaveChangesAsync();
            return k;
        }

        public CreateOrderHandler Handler(Guid? prijavljen) =>
            new(T.Db, new FakeCurrentUser(prijavljen), new FakeEmail(), new FakeViber(),
                Options.Create(new KorpiceOpcije()), Options.Create(new PretplateOpcije()));

        public CreateOrderCommand Komanda(string? kod, decimal kolicina = 1m) =>
            new(new CustomerInput("Mujo", "mujo@example.com", "065"), Zona.Id, Slot.Id, "Testna 1", null,
                [new OrderItemInput(Proizvod.Id, kolicina)], PromoKod: kod);

        public Task ZahtjevAsync(string? kod, string email = "gost@example.com") =>
            new CreateGuestOrderRequestHandler(T.Db, new FakeCurrentUser(null), new FakeEmail())
                .Handle(new CreateGuestOrderRequestCommand(
                    new CustomerInput("Gost", email, "065"), Zona.Id, Slot.Id, "Testna 1", null,
                    [new OrderItemInput(Proizvod.Id, 1m)], PromoKod: kod), default);

        public Task<EPiljara.Application.DTOs.OrderDto> PotvrdiAsync(string token) =>
            new ConfirmGuestOrderHandler(T.Db, new FakeEmail(), new FakeViber(), Options.Create(new KorpiceOpcije()))
                .Handle(new ConfirmGuestOrderCommand(token), default);

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Prijavljen_kupac_sa_kodom_dobija_popust_na_robu()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kod = await o.KodAsync();

        var dto = await o.Handler(Guid.NewGuid()).Handle(o.Komanda("jesen5"), default);

        dto.Popust.Should().Be(5m);
        dto.PromoKod.Should().Be("JESEN5");
        o.T.Db.ChangeTracker.Clear();
        var order = await o.T.Db.Orders.AsNoTracking().SingleAsync();
        order.PromoKodId.Should().Be(kod.Id);
        order.Popust.Should().Be(5m);
        order.TotalAmount.Should().Be(order.Subtotal - 5m + order.DeliveryFee);
        order.TotalAmount.Should().Be(18m);
    }

    [Fact]
    public async Task Bez_koda_nema_popusta()
    {
        using var o = await Okruzenje.NapraviAsync();

        var dto = await o.Handler(Guid.NewGuid()).Handle(o.Komanda(null), default);

        dto.Popust.Should().Be(0m);
        dto.PromoKod.Should().BeNull();
        dto.TotalAmount.Should().Be(23m);
    }

    [Fact]
    public async Task Nevazeci_kod_pada_ne_upisuje_narudzbu_i_ne_skida_zalihe()
    {
        using var o = await Okruzenje.NapraviAsync();

        var radnja = () => o.Handler(Guid.NewGuid()).Handle(o.Komanda("NEMA"), default);

        (await radnja.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("promo_kod_nevazeci");
        o.T.Db.ChangeTracker.Clear();
        (await o.T.Db.Orders.CountAsync()).Should().Be(0);
        (await o.T.Db.Products.AsNoTracking().SingleAsync()).Stock.Should().Be(100);
    }

    [Fact]
    public async Task Minimum_zone_se_mjeri_prije_popusta()
    {
        using var o = await Okruzenje.NapraviAsync(minimum: 30m, cijena: 30m);
        await o.KodAsync(k => { k.Tip = TipPopusta.Procenat; k.Vrijednost = 20m; });

        var dto = await o.Handler(Guid.NewGuid()).Handle(o.Komanda("JESEN5"), default);

        dto.Popust.Should().Be(6m);
        dto.TotalAmount.Should().Be(27m);
    }

    [Fact]
    public async Task Gost_zahtjev_snima_popust_a_potvrda_prenosi_iste_brojeve()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kod = await o.KodAsync();

        await o.ZahtjevAsync("jesen5");

        var zahtjev = await o.T.Db.GuestOrderRequests.SingleAsync();
        zahtjev.Popust.Should().Be(5m);
        zahtjev.PromoKod.Should().Be("JESEN5");
        zahtjev.PromoKodId.Should().Be(kod.Id);
        zahtjev.TotalAmount.Should().Be(18m);

        var dto = await o.PotvrdiAsync(zahtjev.Token);

        dto.Popust.Should().Be(5m);
        dto.PromoKod.Should().Be("JESEN5");
        dto.TotalAmount.Should().Be(18m);
        (await o.T.Db.Orders.SingleAsync()).PromoKodId.Should().Be(kod.Id);
    }

    [Fact]
    public async Task Gost_jednom_po_kupcu_drugu_potvrdu_odbija_i_brise_zahtjev()
    {
        using var o = await Okruzenje.NapraviAsync();
        await o.KodAsync(k => k.JednomPoKupcu = true);

        // Oba zahtjeva nastaju dok kod još nije iskorišten.
        await o.ZahtjevAsync("JESEN5");
        await o.ZahtjevAsync("JESEN5");
        var tokeni = await o.T.Db.GuestOrderRequests.OrderBy(r => r.CreatedAt).Select(r => r.Token).ToListAsync();

        await o.PotvrdiAsync(tokeni[0]);
        var druga = () => o.PotvrdiAsync(tokeni[1]);

        (await druga.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("promo_kod_nevazeci");
        o.T.Db.ChangeTracker.Clear();
        (await o.T.Db.GuestOrderRequests.CountAsync()).Should().Be(0);
        (await o.T.Db.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Gost_kod_ugasen_izmedju_zahtjeva_i_potvrde_daje_409_i_brise_zahtjev()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kod = await o.KodAsync();
        await o.ZahtjevAsync("JESEN5");
        var token = (await o.T.Db.GuestOrderRequests.SingleAsync()).Token;

        kod.Aktivan = false;
        await o.T.Db.SaveChangesAsync();
        var radnja = () => o.PotvrdiAsync(token);

        (await radnja.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("promo_kod_nevazeci");
        o.T.Db.ChangeTracker.Clear();
        (await o.T.Db.GuestOrderRequests.CountAsync()).Should().Be(0);
        (await o.T.Db.Orders.CountAsync()).Should().Be(0);
        (await o.T.Db.Products.AsNoTracking().SingleAsync()).Stock.Should().Be(100);
    }

    [Fact]
    public void Zicani_oblik_nema_CustomerId_ni_PretplataId()
    {
        var imena = typeof(CreateOrderRequest).GetProperties().Select(p => p.Name);

        imena.Should().NotContain(["CustomerId", "PretplataId"]);
    }

    [Fact]
    public void Tudji_customerId_i_pretplataId_iz_tijela_se_ignorisu()
    {
        var json = $$"""
            {"customer":{"name":"Mujo","email":"m@example.com","phone":"065"},
             "customerId":"{{Guid.NewGuid()}}","pretplataId":"{{Guid.NewGuid()}}",
             "deliveryZoneId":"{{Guid.NewGuid()}}","deliverySlotId":"{{Guid.NewGuid()}}",
             "deliveryAddress":"Testna 1","note":null,"items":[],"promoKod":"JESEN5"}
            """;

        var request = JsonSerializer.Deserialize<CreateOrderRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var cmd = request.ToCommand();

        cmd.CustomerId.Should().BeNull();
        cmd.PretplataId.Should().BeNull();
        cmd.PromoKod.Should().Be("JESEN5");
    }

    [Fact]
    public void Sablon_mejla_i_viber_prikazuju_popust_samo_kad_ga_ima()
    {
        var sa = new Order
        {
            Code = "EP-1", CustomerName = "Mujo", DeliveryAddress = "Testna 1",
            Subtotal = 20m, DeliveryFee = 3m, Popust = 5m, PromoKod = "JESEN5", TotalAmount = 18m,
        };
        var bez = new Order
        {
            Code = "EP-2", CustomerName = "Mujo", DeliveryAddress = "Testna 1",
            Subtotal = 20m, DeliveryFee = 3m, TotalAmount = 23m,
        };

        OrderEmailTemplate.Build(sa).Should().Contain("JESEN5").And.Contain("KM 5,00");
        OrderEmailTemplate.Build(bez).Should().NotContain("Popust");
        ViberMessage.Build(sa).Should().Contain("Popust (JESEN5): −KM 5,00");
        ViberMessage.Build(bez).Should().NotContain("Popust");

        var zahtjev = new GuestOrderRequest
        {
            Subtotal = 20m, DeliveryFee = 3m, Popust = 5m, PromoKod = "JESEN5", TotalAmount = 18m,
        };
        GuestOrderEmailTemplate.Build(zahtjev, "https://x/y").Should().Contain("JESEN5").And.Contain("KM 5,00");
        GuestOrderEmailTemplate.Build(new GuestOrderRequest { TotalAmount = 23m }, "https://x/y")
            .Should().NotContain("Popust");
    }
}
