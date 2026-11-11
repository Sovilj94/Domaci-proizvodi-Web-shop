using EPiljara.Application.Features.Driver;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Kad vozač završi rutu, sljedeća je ona najbliža njegovoj trenutnoj poziciji.</summary>
public class SljedecaRutaTests
{
    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public User Korisnik { get; } = new() { Name = "Mladen Kovačević", Email = "vozac@test.local", PasswordHash = "x", Role = UserRole.Driver };
        public Driver Vozac { get; }
        public DeliveryZone Zona { get; } = new() { Name = "Borik", IsActive = true };
        public DeliverySlot Slot { get; } = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        private int _broj;

        public Okruzenje()
        {
            Vozac = new Driver { Name = "Mladen Kovačević", Phone = "065 111 222", Aktivan = true, UserId = Korisnik.Id };
            T.Db.AddRange(Korisnik, Vozac, Zona, Slot);
            T.Db.SaveChanges();
        }

        public DeliveryRoute Ruta(string naziv, int redni, params (double Lat, double Lon, OrderStatus Status)[] stanice)
        {
            var r = new DeliveryRoute
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow), DriverId = Vozac.Id, Naziv = naziv,
                DeliveryZoneId = Zona.Id, RedniBroj = redni,
            };
            var i = 0;
            foreach (var (lat, lon, status) in stanice)
            {
                var o = new Order
                {
                    Code = $"EP-S-{++_broj:D4}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac",
                    Status = status, DeliveryZoneId = Zona.Id, DeliverySlotId = Slot.Id, DeliveryAddress = "Adresa",
                    Latitude = lat, Longitude = lon, Godina = 2026, Sedmica = 46,
                };
                T.Db.Orders.Add(o);
                r.Stops.Add(new RouteStop { OrderId = o.Id, Sequence = ++i });
            }
            T.Db.DeliveryRoutes.Add(r);
            T.Db.SaveChanges();
            return r;
        }

        public Task<EPiljara.Application.DTOs.DeliveryRouteDto?> SljedecaAsync(double lat, double lon)
        {
            T.Db.ChangeTracker.Clear();
            return new SljedecaRutaHandler(T.Db, new FakeCurrentUser(Korisnik.Id, UserRole.Driver), new NajbliziSusjedOptimizer())
                .Handle(new SljedecaRutaCommand(lat, lon), default);
        }

        public async Task<int?> RedniAsync(Guid id)
        {
            T.Db.ChangeTracker.Clear();
            return (await T.Db.DeliveryRoutes.AsNoTracking().SingleAsync(r => r.Id == id)).RedniBroj;
        }

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Bira_najblizu_poziciji_i_pomjera_redne_brojeve()
    {
        using var o = new Okruzenje();
        var zavrsena = o.Ruta("Borik 1", 1, (44.770, 17.200, OrderStatus.Delivered), (44.771, 17.201, OrderStatus.FailedDelivery));
        var zapad = o.Ruta("Lauš", 2, (44.775, 17.160, OrderStatus.Confirmed), (44.776, 17.161, OrderStatus.Confirmed));
        var istok = o.Ruta("Ada", 3, (44.772, 17.222, OrderStatus.Confirmed));

        var dto = await o.SljedecaAsync(44.771, 17.215);  // vozač je na istoku

        Assert.Equal(istok.Id, dto!.Id);
        Assert.Equal(2, dto.RedniBroj);
        Assert.Equal(1, await o.RedniAsync(zavrsena.Id));
        Assert.Equal(2, await o.RedniAsync(istok.Id));
        Assert.Equal(3, await o.RedniAsync(zapad.Id));
    }

    [Fact]
    public async Task Preracunava_stanice_od_pozicije()
    {
        using var o = new Okruzenje();
        o.Ruta("Borik 1", 1, (44.770, 17.200, OrderStatus.Delivered));
        // Stanice upisane od zapada ka istoku; vozač dolazi sa istoka.
        var r = o.Ruta("Ada", 2, (44.772, 17.212, OrderStatus.Confirmed), (44.772, 17.222, OrderStatus.Confirmed));

        var dto = await o.SljedecaAsync(44.772, 17.230);

        Assert.Equal(r.Stops.OrderBy(s => s.Sequence).Last().OrderId, dto!.Stops.First().OrderId);
        Assert.Equal("vazdusno", dto.IzvorRedoslijeda);
    }

    [Fact]
    public async Task Nema_preostalih_vraca_null()
    {
        using var o = new Okruzenje();
        o.Ruta("Borik 1", 1, (44.770, 17.200, OrderStatus.Delivered));

        Assert.Null(await o.SljedecaAsync(44.771, 17.215));
    }

    [Fact]
    public async Task Bez_ruta_vraca_null()
    {
        using var o = new Okruzenje();

        Assert.Null(await o.SljedecaAsync(44.771, 17.215));
    }

    [Fact]
    public async Task Pozicija_se_ne_cuva()
    {
        using var o = new Okruzenje();
        o.Ruta("Ada", 1, (44.772, 17.222, OrderStatus.Confirmed));
        const double lat = 44.7771234, lon = 17.2154321;

        await o.SljedecaAsync(lat, lon);

        o.T.Db.ChangeTracker.Clear();
        var ruta = await o.T.Db.DeliveryRoutes.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("17.215432", ruta.Geometrija ?? "");
        Assert.False(await o.T.Db.Orders.AnyAsync(x => x.Latitude == lat || x.Longitude == lon));
        Assert.Null(ruta.KilometrazaMetara);
    }

    [Fact]
    public async Task Tacka_van_BiH_se_odbija()
    {
        using var o = new Okruzenje();

        var ex = await Assert.ThrowsAsync<EPiljara.Domain.Common.DomainException>(() => o.SljedecaAsync(0, 0));

        Assert.Equal("invalid_coordinates", ex.Code);
    }
}
