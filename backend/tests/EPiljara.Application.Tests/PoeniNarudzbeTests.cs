using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Application.Features.Poeni;
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

/// <summary>P6c Task 2: trošenje poena pri kreiranju narudžbe.</summary>
public class PoeniNarudzbeTests
{
    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public DeliveryZone Zona { get; set; } = null!;
        public DeliverySlot Slot { get; set; } = null!;
        public Product Proizvod { get; set; } = null!;

        public static async Task<Okruzenje> NapraviAsync()
        {
            var o = new Okruzenje();
            o.Zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
            o.Slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            o.Proizvod = new Product
            {
                Name = "Med", Slug = "med", Price = 20m, Unit = Unit.Kom, Stock = 100, IsAvailable = true,
                Category = new Category { Name = "Med", Slug = "med" },
                Producer = new Producer { Name = "Marko", Village = "Manjača" },
            };
            o.T.Db.DeliveryZones.Add(o.Zona);
            o.T.Db.DeliverySlots.Add(o.Slot);
            o.T.Db.Products.Add(o.Proizvod);
            await o.T.Db.SaveChangesAsync();
            return o;
        }

        public async Task<Guid> KupacSaPoenimaAsync(int poena)
        {
            var u = new User { Name = "Mujo", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x" };
            T.Db.Users.Add(u);
            if (poena != 0)
                T.Db.PoeniStavke.Add(new PoeniStavka { UserId = u.Id, Iznos = poena, Vrsta = VrstaPoena.Korekcija });
            await T.Db.SaveChangesAsync();
            return u.Id;
        }

        public async Task<PromoKod> KodAsync()
        {
            var k = new PromoKod { Kod = "JESEN5", Opis = "Test", Tip = TipPopusta.Iznos, Vrijednost = 5m };
            T.Db.PromoKodovi.Add(k);
            await T.Db.SaveChangesAsync();
            return k;
        }

        public CreateOrderHandler Handler(Guid? prijavljen) =>
            new(T.Db, new FakeCurrentUser(prijavljen), new FakeEmail(), new FakeViber(),
                Options.Create(new KorpiceOpcije()), Options.Create(new PretplateOpcije()),
                Options.Create(new PoeniOpcije()));

        public CreateOrderCommand Komanda(bool poeni, string? kod = null, Guid? customerId = null) =>
            new(new CustomerInput("Mujo", "mujo@example.com", "065"), Zona.Id, Slot.Id, "Testna 1", null,
                [new OrderItemInput(Proizvod.Id, 1m)], CustomerId: customerId, PromoKod: kod, IskoristiPoene: poeni);

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Poeni_se_troše_na_robu_i_pisu_stavku_sa_narudzbom()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(300);

        var dto = await o.Handler(kupac).Handle(o.Komanda(true), default);

        dto.IskoristeniPoeni.Should().Be(300);
        dto.PoeniPopust.Should().Be(15m);
        dto.TotalAmount.Should().Be(8m);
        o.T.Db.ChangeTracker.Clear();
        var stavka = await o.T.Db.PoeniStavke.AsNoTracking().SingleAsync(s => s.Vrsta == VrstaPoena.Iskoristeno);
        stavka.Iznos.Should().Be(-300);
        stavka.UserId.Should().Be(kupac);
        stavka.OrderId.Should().Be(dto.Id);
        (await PoeniPravila.StanjeAsync(o.T.Db, kupac, default)).Should().Be(0);
    }

    [Fact]
    public async Task Poeni_su_ogranicen_robom()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(1000);

        var dto = await o.Handler(kupac).Handle(o.Komanda(true), default);

        dto.IskoristeniPoeni.Should().Be(400);
        dto.PoeniPopust.Should().Be(20m);
        dto.TotalAmount.Should().Be(3m);
    }

    [Fact]
    public async Task Poeni_se_racunaju_poslije_promo_koda()
    {
        using var o = await Okruzenje.NapraviAsync();
        await o.KodAsync();
        var kupac = await o.KupacSaPoenimaAsync(1000);

        var dto = await o.Handler(kupac).Handle(o.Komanda(true, "JESEN5"), default);

        dto.Popust.Should().Be(5m);
        dto.PoeniPopust.Should().Be(15m);
        dto.IskoristeniPoeni.Should().Be(300);
        dto.TotalAmount.Should().Be(20m - 5m - 15m + 3m);
    }

    [Fact]
    public async Task Bez_oznake_poeni_se_ne_troše()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(300);

        var dto = await o.Handler(kupac).Handle(o.Komanda(false), default);

        dto.IskoristeniPoeni.Should().Be(0);
        dto.PoeniPopust.Should().Be(0m);
        dto.TotalAmount.Should().Be(23m);
        (await o.T.Db.PoeniStavke.CountAsync(s => s.Vrsta == VrstaPoena.Iskoristeno)).Should().Be(0);
    }

    [Fact]
    public async Task Stanje_nula_ne_pravi_stavku()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(0);

        var dto = await o.Handler(kupac).Handle(o.Komanda(true), default);

        dto.IskoristeniPoeni.Should().Be(0);
        dto.TotalAmount.Should().Be(23m);
        (await o.T.Db.PoeniStavke.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Komanda_sa_CustomerId_bez_oznake_ne_troši_poene()
    {
        // Pozadinski posao pretplata ima CustomerId a nema ICurrentUser.
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(300);

        var dto = await o.Handler(null).Handle(o.Komanda(false, customerId: kupac), default);

        dto.IskoristeniPoeni.Should().Be(0);
        dto.TotalAmount.Should().Be(23m);
        (await o.T.Db.PoeniStavke.CountAsync(s => s.Vrsta == VrstaPoena.Iskoristeno)).Should().Be(0);
    }

    [Fact]
    public async Task Gost_ne_moze_da_troši_poene()
    {
        using var o = await Okruzenje.NapraviAsync();

        var dto = await o.Handler(null).Handle(o.Komanda(true), default);

        dto.IskoristeniPoeni.Should().Be(0);
        dto.TotalAmount.Should().Be(23m);
        (await o.T.Db.PoeniStavke.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Nevazeci_promo_kod_ne_upisuje_ni_stavku_poena()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = await o.KupacSaPoenimaAsync(300);

        var radnja = () => o.Handler(kupac).Handle(o.Komanda(true, "NEMA"), default);

        await radnja.Should().ThrowAsync<DomainException>();
        o.T.Db.ChangeTracker.Clear();
        (await o.T.Db.Orders.CountAsync()).Should().Be(0);
        (await o.T.Db.PoeniStavke.CountAsync(s => s.Vrsta == VrstaPoena.Iskoristeno)).Should().Be(0);
    }

    [Fact]
    public void Zahtjev_sa_zice_prenosi_IskoristiPoene()
    {
        var zahtjev = new CreateOrderRequest(
            new CustomerInput("Mujo", null, "065"), Guid.NewGuid(), Guid.NewGuid(), "Testna 1", null,
            [], IskoristiPoene: true);

        zahtjev.ToCommand().IskoristiPoene.Should().BeTrue();
    }

    private static Order SaPoenima() => new()
    {
        Code = "EP-1", CustomerName = "Mujo", DeliveryAddress = "Testna 1",
        Subtotal = 20m, DeliveryFee = 3m, IskoristeniPoeni = 300, PoeniPopust = 15m, TotalAmount = 8m,
    };

    private static Order BezPoena() => new()
    {
        Code = "EP-2", CustomerName = "Mujo", DeliveryAddress = "Testna 1",
        Subtotal = 20m, DeliveryFee = 3m, TotalAmount = 23m,
    };

    [Fact]
    public void Mejl_prikazuje_red_poena_samo_kad_su_potroseni()
    {
        OrderEmailTemplate.Build(SaPoenima()).Should().Contain("Poeni (300)").And.Contain("KM 15,00");
        OrderEmailTemplate.Build(BezPoena()).Should().NotContain("Poeni");
    }

    [Fact]
    public void Viber_prikazuje_red_poena_samo_kad_su_potroseni()
    {
        ViberMessage.Build(SaPoenima()).Should().Contain("Poeni (300): −KM 15,00");
        ViberMessage.Build(BezPoena()).Should().NotContain("Poeni");
    }
}
