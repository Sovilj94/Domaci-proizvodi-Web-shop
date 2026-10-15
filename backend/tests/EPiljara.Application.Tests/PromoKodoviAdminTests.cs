using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>Admin CRUD promo kodova (P6b, Task 2).</summary>
public class PromoKodoviAdminTests
{
    private static PromoKodInput Ulaz(
        string kod = "JESEN5", string tip = "Procenat", decimal vrijednost = 10m,
        decimal? minIznos = null, DateOnly? od = null, DateOnly? doo = null, int? max = null) =>
        new(kod, "Jesenja akcija", tip, vrijednost, minIznos, od, doo, max, false, false, true);

    private static void DodajNarudzbu(TestDb t, Guid kodId, OrderStatus status, string code)
    {
        var zona = t.Db.DeliveryZones.FirstOrDefault()
            ?? t.Db.DeliveryZones.Add(new DeliveryZone { Name = "Centar", DeliveryFee = 3m, IsActive = true }).Entity;
        var slot = t.Db.DeliverySlots.FirstOrDefault()
            ?? t.Db.DeliverySlots.Add(new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 }).Entity;
        t.Db.SaveChanges();
        t.Db.Orders.Add(new Order
        {
            Code = code, PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Test",
            Status = status, PromoKodId = kodId, DeliveryAddress = "Testna 1",
            DeliverySlotId = slot.Id, DeliveryZoneId = zona.Id,
        });
        t.Db.SaveChanges();
    }

    [Fact]
    public async Task Lista_broji_koristenja_bez_otkazanih()
    {
        using var t = new TestDb();
        var dto = await new CreatePromoKodHandler(t.Db).Handle(new CreatePromoKodCommand(Ulaz()), default);
        DodajNarudzbu(t, dto.Id, OrderStatus.Pending, "EP-0001");
        DodajNarudzbu(t, dto.Id, OrderStatus.Cancelled, "EP-0002");

        var lista = await new GetAdminPromoKodoviHandler(t.Db).Handle(new GetAdminPromoKodoviQuery(), default);

        Assert.Equal(1, Assert.Single(lista).BrojKoristenja);
    }

    [Fact]
    public async Task Create_normalizuje_kod()
    {
        using var t = new TestDb();

        var dto = await new CreatePromoKodHandler(t.Db).Handle(new CreatePromoKodCommand(Ulaz(" jesen5 ")), default);

        Assert.Equal("JESEN5", dto.Kod);
        Assert.Equal("JESEN5", t.Db.PromoKodovi.Single().Kod);
    }

    [Theory]
    [InlineData("JESEN5")]
    [InlineData("jesen5")]
    public async Task Create_duplikata_baca_conflict(string drugi)
    {
        using var t = new TestDb();
        var h = new CreatePromoKodHandler(t.Db);
        await h.Handle(new CreatePromoKodCommand(Ulaz("JESEN5")), default);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Handle(new CreatePromoKodCommand(Ulaz(drugi)), default));

        Assert.Equal("promo_kod_postoji", ex.Code);
    }

    [Fact]
    public async Task Update_mijenja_vrijednost_ali_ne_kod()
    {
        using var t = new TestDb();
        var dto = await new CreatePromoKodHandler(t.Db).Handle(new CreatePromoKodCommand(Ulaz("JESEN5")), default);

        var rez = await new UpdatePromoKodHandler(t.Db)
            .Handle(new UpdatePromoKodCommand(dto.Id, Ulaz("DRUGI", vrijednost: 20m)), default);

        Assert.Equal("JESEN5", rez.Kod);
        Assert.Equal(20m, rez.Vrijednost);
        Assert.Equal("JESEN5", t.Db.PromoKodovi.Single().Kod);
    }

    [Fact]
    public async Task Delete_nekoristenog_brise()
    {
        using var t = new TestDb();
        var dto = await new CreatePromoKodHandler(t.Db).Handle(new CreatePromoKodCommand(Ulaz()), default);

        await new DeletePromoKodHandler(t.Db).Handle(new DeletePromoKodCommand(dto.Id), default);

        Assert.Empty(t.Db.PromoKodovi);
    }

    [Fact]
    public async Task Delete_koristenog_i_sa_samo_otkazanom_narudzbom_baca_conflict()
    {
        using var t = new TestDb();
        var dto = await new CreatePromoKodHandler(t.Db).Handle(new CreatePromoKodCommand(Ulaz()), default);
        DodajNarudzbu(t, dto.Id, OrderStatus.Cancelled, "EP-0001");

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => new DeletePromoKodHandler(t.Db).Handle(new DeletePromoKodCommand(dto.Id), default));

        Assert.Equal("promo_kod_koristen", ex.Code);
        Assert.Single(t.Db.PromoKodovi);
    }

    [Fact]
    public async Task Delete_nepostojeceg_baca_not_found()
    {
        using var t = new TestDb();

        await Assert.ThrowsAsync<NotFoundException>(
            () => new DeletePromoKodHandler(t.Db).Handle(new DeletePromoKodCommand(Guid.NewGuid()), default));
    }

    [Theory]
    [InlineData("AB", "Procenat", 10, "input.kod", "Kod mora imati 3 do 30 znakova: slova A–Z, cifre i crtica.")]
    [InlineData("ŠUMA", "Procenat", 10, "input.kod", "Kod mora imati 3 do 30 znakova: slova A–Z, cifre i crtica.")]
    [InlineData("JESEN5", "Pola", 10, "input.tip", "Tip popusta mora biti Procenat ili Iznos.")]
    [InlineData("JESEN5", "Procenat", 0, "input.vrijednost", "Procenat mora biti između 1 i 90.")]
    [InlineData("JESEN5", "Procenat", 91, "input.vrijednost", "Procenat mora biti između 1 i 90.")]
    [InlineData("JESEN5", "Iznos", 0, "input.vrijednost", "Iznos popusta mora biti između 0,01 i 1000 KM.")]
    [InlineData("JESEN5", "Iznos", 1001, "input.vrijednost", "Iznos popusta mora biti između 0,01 i 1000 KM.")]
    public void Validator_odbija_neispravno_polje(string kod, string tip, decimal vrijednost, string kljuc, string poruka)
    {
        var r = new CreatePromoKodValidator().Validate(new CreatePromoKodCommand(Ulaz(kod, tip, vrijednost)));

        var greske = new ValidationException(r.Errors).Errors;
        Assert.Equal(poruka, Assert.Single(greske[kljuc]));
    }

    [Fact]
    public void Validator_odbija_datume_limit_i_minimum()
    {
        var r = new CreatePromoKodValidator().Validate(new CreatePromoKodCommand(Ulaz(
            od: new DateOnly(2026, 10, 20), doo: new DateOnly(2026, 10, 10), max: 0, minIznos: -1m)));

        var greske = new ValidationException(r.Errors).Errors;
        Assert.Equal("Kraj važenja ne može biti prije početka.", Assert.Single(greske["input.vaziDo"]));
        Assert.Equal("Limit korišćenja mora biti bar 1.", Assert.Single(greske["input.maxKoristenja"]));
        Assert.Equal("Minimalni iznos ne može biti negativan.", Assert.Single(greske["input.minIznos"]));
    }

    [Fact]
    public void Validator_prihvata_ispravan_ulaz_i_odbija_null()
    {
        Assert.True(new CreatePromoKodValidator().Validate(new CreatePromoKodCommand(Ulaz(" jesen5 "))).IsValid);

        var r = new CreatePromoKodValidator().Validate(new CreatePromoKodCommand(null!));
        Assert.False(r.IsValid);
        Assert.Equal("Podaci o kodu su obavezni.", r.Errors.Single().ErrorMessage);
    }

    [Fact]
    public void Update_validator_prihvata_ispravan_ulaz_sa_postojecim_kodom()
    {
        Assert.True(new UpdatePromoKodValidator().Validate(new UpdatePromoKodCommand(Guid.NewGuid(), Ulaz())).IsValid);
    }
}
