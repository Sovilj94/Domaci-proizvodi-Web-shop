using EPiljara.Application.Features.Naselja;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Admin stranica „Naselja": granice, aktivnost, preklapanje i preračun naselja.</summary>
public class AdminNaseljaTests
{
    // Borik: 17.20–17.21, Ada: 17.21–17.22 (dijele ivicu); tačka narudžbe je u Boriku blizu Ade.
    private static readonly string BorikG = GranicaNaseljaTests.Kvadrat(17.20, 44.76, 17.21, 44.78);
    private static readonly string AdaG = GranicaNaseljaTests.Kvadrat(17.21, 44.76, 17.22, 44.78);
    // Borik proširen preko tačke 17.215 → preklapa Adu.
    private static readonly string BorikSiri = GranicaNaseljaTests.Kvadrat(17.20, 44.76, 17.218, 44.78);
    // Ada proširena preko tačke 17.208 — ne smije se sačuvati dok Borik postoji, pa se Borik prvo suzi.
    private static readonly string BorikUzi = GranicaNaseljaTests.Kvadrat(17.20, 44.76, 17.205, 44.78);
    private static readonly string AdaSira = GranicaNaseljaTests.Kvadrat(17.205, 44.76, 17.22, 44.78);

    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public DeliveryZone Borik { get; private set; } = null!;
        public DeliveryZone Ada { get; private set; } = null!;
        public DeliverySlot Slot { get; private set; } = null!;

        public static async Task<Okruzenje> NapraviAsync()
        {
            var o = new Okruzenje();
            o.Borik = new DeliveryZone { Name = "Borik", IsActive = true, Granica = BorikG };
            o.Ada = new DeliveryZone { Name = "Ada", IsActive = true, Granica = AdaG };
            o.Slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            o.T.Db.AddRange(o.Borik, o.Ada, o.Slot);
            await o.T.Db.SaveChangesAsync();
            return o;
        }

        public async Task<Order> NarudzbaAsync(double lon, OrderStatus status = OrderStatus.Confirmed, decimal dostava = 5m)
        {
            var n = new Order
            {
                Code = "EP-" + Guid.NewGuid().ToString("N")[..6], PublicToken = Guid.NewGuid().ToString("N"),
                CustomerName = "Dragan Petrović", CustomerPhone = "065 111 222", Status = status,
                DeliveryZoneId = Borik.Id, DeliverySlotId = Slot.Id, DeliveryAddress = "Reljina 18",
                Latitude = 44.77, Longitude = lon, Subtotal = 20m, DeliveryFee = dostava, TotalAmount = 20m + dostava,
            };
            T.Db.Orders.Add(n);
            await T.Db.SaveChangesAsync();
            return n;
        }

        public Task<SacuvajNaseljeRezultat> SacuvajAsync(DeliveryZone z, string? granica = null, bool? aktivno = null,
            bool potvrdi = true) =>
            new SacuvajNaseljeHandler(T.Db).Handle(new SacuvajNaseljeCommand(
                z.Id, z.Name, aktivno ?? z.IsActive, granica ?? z.Granica, null, null, false, potvrdi), default);

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Cuvanje_odbija_preklapanje()
    {
        using var o = await Okruzenje.NapraviAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.SacuvajAsync(o.Borik, granica: BorikSiri));

        Assert.Equal("naselja_se_preklapaju", ex.Code);
        Assert.Contains("Ada", ex.Message);
    }

    [Fact]
    public async Task Rupa_izmedju_naselja_je_dozvoljena()
    {
        using var o = await Okruzenje.NapraviAsync();

        var r = await o.SacuvajAsync(o.Borik, granica: BorikUzi);

        Assert.True(r.Sacuvano);
    }

    [Fact]
    public async Task Novo_naselje_dobija_centar_iz_granice()
    {
        using var o = await Okruzenje.NapraviAsync();

        var r = await new SacuvajNaseljeHandler(o.T.Db).Handle(new SacuvajNaseljeCommand(
            null, "Lauš", true, GranicaNaseljaTests.Kvadrat(17.16, 44.77, 17.17, 44.78), null, null, false, true), default);

        Assert.True(r.Sacuvano);
        Assert.InRange(r.Naselje!.CentarLat!.Value, 44.77, 44.78);
        Assert.Equal(IzvoriGranice.Procjena, r.Naselje.IzvorGranice);
    }

    [Fact]
    public async Task Isti_naziv_je_konflikt()
    {
        using var o = await Okruzenje.NapraviAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new SacuvajNaseljeHandler(o.T.Db).Handle(
            new SacuvajNaseljeCommand(null, " borik ", true, null, null, null, false, true), default));

        Assert.Equal("naselje_postoji", ex.Code);
    }

    [Fact]
    public async Task Izmjena_granice_trazi_potvrdu_uticaja()
    {
        using var o = await Okruzenje.NapraviAsync();
        await o.NarudzbaAsync(17.207);
        await o.SacuvajAsync(o.Borik, granica: BorikUzi);           // Borik se suzi, narudžba ostaje van granica
        o.T.Db.ChangeTracker.Clear();

        var r = await o.SacuvajAsync(o.Ada, granica: AdaSira, potvrdi: false);

        Assert.False(r.Sacuvano);
        Assert.Equal(1, r.Uticaj!.Narudzbe);
        Assert.Equal(AdaG, (await o.T.Db.DeliveryZones.AsNoTracking().SingleAsync(z => z.Id == o.Ada.Id)).Granica);
    }

    [Fact]
    public async Task Preracun_premjesta_otvorenu_narudzbu_van_rute()
    {
        using var o = await Okruzenje.NapraviAsync();
        var n = await o.NarudzbaAsync(17.207);
        await o.SacuvajAsync(o.Borik, granica: BorikUzi);
        o.T.Db.ChangeTracker.Clear();

        await o.SacuvajAsync(o.Ada, granica: AdaSira);

        Assert.Equal(o.Ada.Id, (await o.T.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == n.Id)).DeliveryZoneId);
    }

    [Fact]
    public async Task Preracun_ne_dira_narudzbu_na_ruti()
    {
        using var o = await Okruzenje.NapraviAsync();
        var n = await o.NarudzbaAsync(17.207);
        var vozac = new Driver { Name = "Mladen", Aktivan = true };
        o.T.Db.Drivers.Add(vozac);
        o.T.Db.DeliveryRoutes.Add(new DeliveryRoute
        {
            Date = new DateOnly(2026, 11, 12), DriverId = vozac.Id, Naziv = "Borik", DeliveryZoneId = o.Borik.Id,
            Stops = [new RouteStop { OrderId = n.Id, Sequence = 1 }],
        });
        await o.T.Db.SaveChangesAsync();
        await o.SacuvajAsync(o.Borik, granica: BorikUzi);
        o.T.Db.ChangeTracker.Clear();

        await o.SacuvajAsync(o.Ada, granica: AdaSira);

        Assert.Equal(o.Borik.Id, (await o.T.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == n.Id)).DeliveryZoneId);
    }

    [Fact]
    public async Task Preracun_ne_mijenja_cijenu()
    {
        using var o = await Okruzenje.NapraviAsync();
        var n = await o.NarudzbaAsync(17.207, dostava: 3m);
        await o.SacuvajAsync(o.Borik, granica: BorikUzi);
        o.T.Db.ChangeTracker.Clear();

        await o.SacuvajAsync(o.Ada, granica: AdaSira);

        var poslije = await o.T.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == n.Id);
        Assert.Equal(3m, poslije.DeliveryFee);
        Assert.Equal(23m, poslije.TotalAmount);
    }

    [Fact]
    public async Task Preracun_premjesta_sacuvanu_adresu()
    {
        using var o = await Okruzenje.NapraviAsync();
        var kupac = new User { Name = "Ana", Email = "ana@test.local", PasswordHash = "x" };
        var adresa = new Address { User = kupac, FullAddress = "Reljina 18", Latitude = 44.77, Longitude = 17.207, DeliveryZoneId = o.Borik.Id };
        o.T.Db.Addresses.Add(adresa);
        await o.T.Db.SaveChangesAsync();
        await o.SacuvajAsync(o.Borik, granica: BorikUzi);
        o.T.Db.ChangeTracker.Clear();

        await o.SacuvajAsync(o.Ada, granica: AdaSira);

        Assert.Equal(o.Ada.Id, (await o.T.Db.Addresses.AsNoTracking().SingleAsync()).DeliveryZoneId);
    }

    [Fact]
    public async Task Iskljuceno_naselje_narudzba_ostaje_i_vodi_se_van_granica()
    {
        using var o = await Okruzenje.NapraviAsync();
        var n = await o.NarudzbaAsync(17.205);
        o.T.Db.ChangeTracker.Clear();

        var r = await o.SacuvajAsync(o.Borik, aktivno: false);

        Assert.True(r.Sacuvano);
        Assert.Equal(o.Borik.Id, (await o.T.Db.Orders.AsNoTracking().SingleAsync(x => x.Id == n.Id)).DeliveryZoneId);
        var spisak = await new GetAdminNaseljaHandler(o.T.Db).Handle(new GetAdminNaseljaQuery(), default);
        var borik = spisak.Single(x => x.Id == o.Borik.Id);
        Assert.Equal(1, borik.OtvorenihNarudzbi);
        Assert.Equal(1, borik.VanGranica);
    }

    [Fact]
    public async Task Spisak_broji_otvorene_i_zna_da_ima_narudzbi()
    {
        using var o = await Okruzenje.NapraviAsync();
        await o.NarudzbaAsync(17.205);
        await o.NarudzbaAsync(17.205, OrderStatus.Delivered);

        var spisak = await new GetAdminNaseljaHandler(o.T.Db).Handle(new GetAdminNaseljaQuery(), default);

        var borik = spisak.Single(x => x.Name == "Borik");
        Assert.Equal(1, borik.OtvorenihNarudzbi);
        Assert.Equal(0, borik.VanGranica);
        Assert.True(borik.ImaNarudzbi);
        Assert.False(spisak.Single(x => x.Name == "Ada").ImaNarudzbi);
    }

    [Fact]
    public async Task Brisanje_naselja_sa_narudzbama_se_odbija()
    {
        using var o = await Okruzenje.NapraviAsync();
        await o.NarudzbaAsync(17.205, OrderStatus.Delivered);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => new ObrisiNaseljeHandler(o.T.Db).Handle(new ObrisiNaseljeCommand(o.Borik.Id), default));

        Assert.Equal("naselje_ima_narudzbe", ex.Code);
    }

    [Fact]
    public async Task Brisanje_naselja_bez_narudzbi()
    {
        using var o = await Okruzenje.NapraviAsync();

        await new ObrisiNaseljeHandler(o.T.Db).Handle(new ObrisiNaseljeCommand(o.Ada.Id), default);

        Assert.False(await o.T.Db.DeliveryZones.AnyAsync(z => z.Id == o.Ada.Id));
    }

    [Fact]
    public async Task Potvrdjena_granica_se_oznacava()
    {
        using var o = await Okruzenje.NapraviAsync();

        var r = await new SacuvajNaseljeHandler(o.T.Db).Handle(new SacuvajNaseljeCommand(
            o.Borik.Id, "Borik", true, BorikG, null, null, true, true), default);

        Assert.Equal(IzvoriGranice.Potvrdjeno, r.Naselje!.IzvorGranice);
    }
}
