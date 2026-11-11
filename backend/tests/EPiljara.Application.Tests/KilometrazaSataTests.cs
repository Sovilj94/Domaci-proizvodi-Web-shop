using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Voznje;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Vozač unosi stanje sata kad krene i kad se vrati u magacin; pređeni km su razlika. Kad su poznati,
/// procjena goriva se računa iz njih umjesto iz procijenjene ture.
/// </summary>
public class KilometrazaSataTests
{
    private sealed class FiksniOptimizer : IRouteOptimizer
    {
        public Task<RouteOptimizationResult> OptimizeAsync(double startLat, double startLon,
            IReadOnlyList<RouteStopInput> stops, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteOptimizationResult(stops.Select(s => s.OrderId).ToList(), 40_000, 3600, null));
    }

    private static readonly DateOnly Danas = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public User Korisnik { get; } = new() { Name = "Mladen Kovačević", Email = "mladen@example.com", Role = UserRole.Driver };
        public Driver Vozac { get; }
        private readonly DeliveryZone _zona = new() { Name = "Centar" };
        private readonly DeliverySlot _slot = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };

        public Okruzenje()
        {
            T.Db.Database.ExecuteSqlRaw(
                $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', 44.77, 17.15)");
            Vozac = new Driver { Name = "Mladen Kovačević", Phone = "065 111 222", UserId = Korisnik.Id };
            T.Db.AddRange(Korisnik, Vozac, _zona, _slot);
            T.Db.CijeneGoriva.Add(new CijenaGoriva { VaziOd = new DateOnly(2020, 1, 1), CijenaPoLitru = 2.50m, PotrosnjaL100km = 8m });
            T.Db.SaveChanges();
        }

        public void Dan(DateOnly datum)
        {
            var ruta = new DeliveryRoute { Date = datum, DriverId = Vozac.Id, Naziv = "Centar", DeliveryZoneId = _zona.Id };
            var n = new Order
            {
                Code = $"EP-K-{datum:MMdd}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac",
                Status = OrderStatus.Confirmed, DeliveryZoneId = _zona.Id, DeliverySlotId = _slot.Id, Godina = 2026, Sedmica = 46,
                Latitude = 44.77, Longitude = 17.19, DeliveryAddress = "Ulica 1",
            };
            T.Db.AddRange(ruta, n);
            T.Db.RouteStops.Add(new RouteStop { RouteId = ruta.Id, OrderId = n.Id, Sequence = 1 });
            T.Db.SaveChanges();
            T.Db.ChangeTracker.Clear();
        }

        public Task<VoznjaDto> MojaAsync(DateOnly datum, int? pocetak, int? kraj)
        {
            T.Db.ChangeTracker.Clear();
            return new SacuvajMojuKilometrazuHandler(T.Db, new FakeCurrentUser(Korisnik.Id, UserRole.Driver), new FiksniOptimizer())
                .Handle(new SacuvajMojuKilometrazuCommand(datum, pocetak, kraj), default);
        }

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Pocetak_pa_kraj_daju_predjene_km()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);

        var ujutru = await o.MojaAsync(Danas, 184_445, null);
        var navece = await o.MojaAsync(Danas, 184_445, 184_532);

        Assert.Equal(184_445, ujutru.KmPocetak);
        Assert.Null(ujutru.PredjenoKm);
        Assert.Equal(184_532, navece.KmKraj);
        Assert.Equal(87, navece.PredjenoKm);
    }

    [Fact]
    public async Task Gorivo_se_racuna_iz_predjenih_km()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);

        var dto = await o.MojaAsync(Danas, 1_000, 1_100);

        // 100 km × 8 l/100 km × 2,50 KM = 20 KM (procjena ture je 40 km → bila bi 8 KM).
        Assert.Equal(20m, dto.ProcjenaGoriva);
        Assert.Equal(8m, dto.ProcjenaLitara);
    }

    [Fact]
    public async Task Kraj_manji_od_pocetka_se_odbija()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.MojaAsync(Danas, 184_445, 184_400));

        Assert.Equal("kilometraza_neispravna", ex.Code);
    }

    [Fact]
    public async Task Kraj_bez_pocetka_se_odbija()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.MojaAsync(Danas, null, 184_532));

        Assert.Equal("kilometraza_neispravna", ex.Code);
        Assert.Equal("Prvo unesi stanje sata na polasku.", ex.Message);
    }

    [Fact]
    public async Task Vise_od_1000_km_u_danu_se_odbija()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.MojaAsync(Danas, 184_445, 185_446));

        Assert.Equal("kilometraza_neispravna", ex.Code);
    }

    [Fact]
    public async Task Pocetak_manji_od_jucerasnjeg_kraja_se_odbija()
    {
        using var o = new Okruzenje();
        o.Dan(Danas.AddDays(-1));
        o.Dan(Danas);
        await o.MojaAsync(Danas.AddDays(-1), 184_445, 184_600);

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.MojaAsync(Danas, 184_500, null));

        Assert.Equal("kilometraza_neispravna", ex.Code);
        Assert.Contains("184.600", ex.Message);
    }

    [Fact]
    public async Task Brisanje_pocetka_brise_i_kraj_ne_moze()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);
        await o.MojaAsync(Danas, 184_445, 184_532);

        var dto = await o.MojaAsync(Danas, null, null);

        Assert.Null(dto.KmPocetak);
        Assert.Null(dto.KmKraj);
        Assert.Null(dto.PredjenoKm);
    }

    [Fact]
    public async Task Dan_bez_dostava_nema_kilometrazu()
    {
        using var o = new Okruzenje();

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.MojaAsync(Danas, 184_445, null));

        Assert.Equal("nema_voznje", ex.Code);
    }

    [Fact]
    public async Task Admin_ispravlja_kilometrazu()
    {
        using var o = new Okruzenje();
        o.Dan(Danas);
        var dto = await o.MojaAsync(Danas, 184_445, 184_532);

        o.T.Db.ChangeTracker.Clear();
        var ispravljen = await new SacuvajKilometrazuVoznjeHandler(o.T.Db)
            .Handle(new SacuvajKilometrazuVoznjeCommand(dto.Id, 184_445, 184_545), default);

        Assert.Equal(100, ispravljen.PredjenoKm);
    }
}
