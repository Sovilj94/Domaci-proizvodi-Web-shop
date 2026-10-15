using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Ocjene;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ValidationException = EPiljara.Application.Common.Exceptions.ValidationException;

namespace EPiljara.Application.Tests;

/// <summary>Ocjena isporuke (P6a, Task 1): vlasnik i gost preko tokena, validator, OrderDto.</summary>
public class OcjeneTests
{
    private static async Task<(TestDb t, Order order, Guid kupac)> PripremiAsync(
        OrderStatus status = OrderStatus.Delivered)
    {
        var t = new TestDb();
        var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        t.Db.DeliveryZones.Add(zona);
        t.Db.DeliverySlots.Add(slot);
        await t.Db.SaveChangesAsync();

        var kupac = Guid.NewGuid();
        var order = new Order
        {
            Code = "EP-0001", PublicToken = Guid.NewGuid().ToString("N"), CustomerId = kupac,
            CustomerName = "Marko Marković", Status = status, DeliveryAddress = "Testna 1",
            DeliverySlotId = slot.Id, DeliveryZoneId = zona.Id,
        };
        t.Db.Orders.Add(order);
        await t.Db.SaveChangesAsync();
        return (t, order, kupac);
    }

    private static OcijeniNarudzbuHandler Vlasnik(TestDb t, Guid? id) => new(t.Db, new FakeCurrentUser(id));

    [Fact]
    public async Task Vlasnik_ocjenjuje_isporucenu_narudzbu_i_komentar_se_trimuje()
    {
        var (t, order, kupac) = await PripremiAsync();
        using var _ = t;

        var dto = await Vlasnik(t, kupac).Handle(
            new OcijeniNarudzbuCommand(order.Id, new OcjenaInput(4, "  Sve super  ", true)), default);

        Assert.Equal(4, dto.Ocjena);
        Assert.Equal("Sve super", dto.Komentar);
        Assert.True(dto.DozvolaZaObjavu);
        t.Db.ChangeTracker.Clear();
        var upisana = await t.Db.OcjeneIsporuke.SingleAsync();
        Assert.Equal(order.Id, upisana.OrderId);
        Assert.Equal("Sve super", upisana.Komentar);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.FailedDelivery)]
    public async Task Neisporucena_narudzba_se_ne_ocjenjuje(OrderStatus status)
    {
        var (t, order, kupac) = await PripremiAsync(status);
        using var _ = t;

        var ex = await Assert.ThrowsAsync<DomainException>(() => Vlasnik(t, kupac).Handle(
            new OcijeniNarudzbuCommand(order.Id, new OcjenaInput(5, null, false)), default));

        Assert.Equal("narudzba_nije_isporucena", ex.Code);
    }

    [Fact]
    public async Task Druga_ocjena_iste_narudzbe_je_konflikt()
    {
        var (t, order, kupac) = await PripremiAsync();
        using var _ = t;
        var cmd = new OcijeniNarudzbuCommand(order.Id, new OcjenaInput(5, null, false));
        await Vlasnik(t, kupac).Handle(cmd, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Vlasnik(t, kupac).Handle(cmd, default));

        Assert.Equal("vec_ocijenjeno", ex.Code);
    }

    [Fact]
    public async Task Tudja_ili_nepostojeca_narudzba_je_404()
    {
        var (t, order, kupac) = await PripremiAsync();
        using var _ = t;
        var input = new OcjenaInput(5, null, false);

        await Assert.ThrowsAsync<NotFoundException>(() => Vlasnik(t, Guid.NewGuid())
            .Handle(new OcijeniNarudzbuCommand(order.Id, input), default));
        await Assert.ThrowsAsync<NotFoundException>(() => Vlasnik(t, kupac)
            .Handle(new OcijeniNarudzbuCommand(Guid.NewGuid(), input), default));
        Assert.Empty(await t.Db.OcjeneIsporuke.ToListAsync());
    }

    [Fact]
    public async Task Gost_ocjenjuje_preko_tokena_a_pogresan_token_je_404()
    {
        var (t, order, _) = await PripremiAsync();
        using var _ = t;
        var h = new OcijeniPrekoTokenaHandler(t.Db);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(
            new OcijeniPrekoTokenaCommand("pogresan", new OcjenaInput(3, null, false)), default));
        var dto = await h.Handle(
            new OcijeniPrekoTokenaCommand(order.PublicToken, new OcjenaInput(3, "Okej", false)), default);

        Assert.Equal(3, dto.Ocjena);
        Assert.Equal("Okej", dto.Komentar);
        Assert.Single(await t.Db.OcjeneIsporuke.ToListAsync());
    }

    [Fact]
    public void Validator_odbija_nevazece_vrijednosti_a_prihvata_ispravne()
    {
        var v = new OcijeniNarudzbuValidator();
        OcijeniNarudzbuCommand K(int o, string? k) => new(Guid.NewGuid(), new OcjenaInput(o, k, false));

        Assert.False(v.Validate(K(0, null)).IsValid);
        Assert.False(v.Validate(K(6, null)).IsValid);
        Assert.False(v.Validate(K(5, new string('x', 1001))).IsValid);
        Assert.True(v.Validate(K(5, new string('x', 1000))).IsValid);
        Assert.True(v.Validate(K(1, null)).IsValid);

        var nul = new ValidationException(v.Validate(
            new OcijeniNarudzbuCommand(Guid.NewGuid(), null!)).Errors);
        Assert.Equal(["input"], nul.Errors.Keys.ToArray());

        var kljucevi = new ValidationException(v.Validate(K(9, new string('x', 2000))).Errors)
            .Errors.Keys.OrderBy(k => k).ToArray();
        Assert.Equal(["input.komentar", "input.ocjena"], kljucevi);
    }

    [Fact]
    public void Validatori_nemaju_engleske_default_poruke()
    {
        var dugo = new string('x', 2000);
        var greske = new OcijeniNarudzbuValidator()
            .Validate(new OcijeniNarudzbuCommand(Guid.Empty, new OcjenaInput(0, dugo, false))).Errors
            .Concat(new OcijeniNarudzbuValidator().Validate(new OcijeniNarudzbuCommand(Guid.Empty, null!)).Errors)
            .Concat(new OcijeniPrekoTokenaValidator()
                .Validate(new OcijeniPrekoTokenaCommand("t", new OcjenaInput(7, dugo, false))).Errors)
            .Concat(new OcijeniPrekoTokenaValidator().Validate(new OcijeniPrekoTokenaCommand("t", null!)).Errors)
            .ToList();

        Assert.NotEmpty(greske);
        Assert.All(greske, e =>
        {
            Assert.DoesNotContain("must", e.ErrorMessage);
            Assert.DoesNotContain("'", e.ErrorMessage);
        });
    }

    public static TheoryData<string> Upiti => ["mine", "byId", "track"];

    private static async Task<OrderDto> Dohvati(string upit, TestDb t, Order o, Guid kupac)
    {
        t.Db.ChangeTracker.Clear();
        return upit switch
        {
            "mine" => (await new GetMyOrdersHandler(t.Db, new FakeCurrentUser(kupac))
                .Handle(new GetMyOrdersQuery(), default)).Single(),
            "byId" => await new GetOrderByIdHandler(t.Db, new FakeCurrentUser(kupac))
                .Handle(new GetOrderByIdQuery(o.Id), default),
            _ => await new TrackOrderHandler(t.Db).Handle(new TrackOrderQuery(o.PublicToken), default),
        };
    }

    [Theory]
    [MemberData(nameof(Upiti))]
    public async Task OrderDto_prati_ocjenu_u_sva_tri_kupceva_upita(string upit)
    {
        var (t, order, kupac) = await PripremiAsync();
        using var _ = t;

        var prije = await Dohvati(upit, t, order, kupac);
        Assert.True(prije.MozeSeOcijeniti);
        Assert.Null(prije.Ocjena);

        await Vlasnik(t, kupac).Handle(
            new OcijeniNarudzbuCommand(order.Id, new OcjenaInput(4, null, false)), default);

        var poslije = await Dohvati(upit, t, order, kupac);
        Assert.False(poslije.MozeSeOcijeniti);
        Assert.Equal(4, poslije.Ocjena!.Ocjena);
    }
}
