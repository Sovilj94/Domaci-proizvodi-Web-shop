using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Driver;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Trošak goriva se računa iz kilometraže cijele ture (magacin → stanice → magacin). Vozačeva
/// ponovna optimizacija od trenutne pozicije računa samo ostatak puta, pa ne smije da je prepiše.
/// </summary>
public class KilometrazaRuteTests
{
    private sealed class FiksniOptimizer(double metara) : IRouteOptimizer
    {
        public Task<RouteOptimizationResult> OptimizeAsync(double startLat, double startLon,
            IReadOnlyList<RouteStopInput> stops, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteOptimizationResult(stops.Select(s => s.OrderId).ToList(), metara, 1800, null));
    }

    private sealed record Osnova(TestDb T, DeliveryRoute Ruta, User Vozac, List<Order> Narudzbe);

    private static Osnova Napravi()
    {
        var t = new TestDb();
        // Sirovi SQL: SQLite provajder piše Guid velikim slovima, a CHECK magacina traži tačan tekst Id-a.
        if (!t.Db.Magacin.Any())
            t.Db.Database.ExecuteSqlRaw(
                $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', 44.77, 17.19)");
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var korisnik = new User { Name = "Dragan Stojanović", Email = "vozac@example.com", Role = UserRole.Driver };
        var vozac = new Driver { Name = "Dragan Stojanović", Phone = "065 111 222", UserId = korisnik.Id };
        var ruta = new DeliveryRoute
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow), DriverId = vozac.Id, Naziv = "Centar", DeliveryZoneId = zona.Id,
        };
        var narudzbe = Enumerable.Range(0, 3).Select(i => new Order
        {
            Code = $"EP-T-{i}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac", Status = OrderStatus.Packing,
            DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id, Godina = 2026, Sedmica = 45,
            Latitude = 44.77 + i * 0.001, Longitude = 17.19,
        }).ToList();
        t.Db.AddRange(slot, zona, korisnik, vozac, ruta);
        t.Db.Orders.AddRange(narudzbe);
        for (var i = 0; i < narudzbe.Count; i++)
            t.Db.RouteStops.Add(new RouteStop { RouteId = ruta.Id, OrderId = narudzbe[i].Id, Sequence = i + 1 });
        t.Db.SaveChanges();
        return new Osnova(t, ruta, korisnik, narudzbe);
    }

    private static async Task<DeliveryRoute> RutaAsync(Osnova o)
    {
        o.T.Db.ChangeTracker.Clear();
        return await o.T.Db.DeliveryRoutes.AsNoTracking().SingleAsync(r => r.Id == o.Ruta.Id);
    }

    [Fact]
    public async Task Optimizacija_od_magacina_upisuje_kilometrazu_cijele_ture()
    {
        var o = Napravi();
        using var _ = o.T;

        await new OptimizeRouteHandler(o.T.Db, new FiksniOptimizer(24_500)).Handle(new OptimizeRouteCommand(o.Ruta.Id), default);

        Assert.Equal(24_500, (await RutaAsync(o)).KilometrazaMetara);
    }

    [Fact]
    public async Task Vozaceva_ponovna_optimizacija_ne_prepisuje_kilometrazu()
    {
        var o = Napravi();
        using var _ = o.T;
        await new OptimizeRouteHandler(o.T.Db, new FiksniOptimizer(24_500)).Handle(new OptimizeRouteCommand(o.Ruta.Id), default);
        o.T.Db.ChangeTracker.Clear();

        await new ReoptimizeRouteHandler(o.T.Db, new FakeCurrentUser(o.Vozac.Id, UserRole.Driver), new FiksniOptimizer(6_000))
            .Handle(new ReoptimizeRouteCommand(44.775, 17.19), default);

        var ruta = await RutaAsync(o);
        Assert.Equal(6_000, ruta.DuzinaMetara);
        Assert.Equal(24_500, ruta.KilometrazaMetara);
    }

    [Fact]
    public async Task Optimizacija_poslije_isporucene_stanice_ne_mijenja_kilometrazu()
    {
        var o = Napravi();
        using var _ = o.T;
        await new OptimizeRouteHandler(o.T.Db, new FiksniOptimizer(24_500)).Handle(new OptimizeRouteCommand(o.Ruta.Id), default);
        o.T.Db.ChangeTracker.Clear();
        var prva = await o.T.Db.Orders.FirstAsync(x => x.Id == o.Narudzbe[0].Id);
        prva.Status = OrderStatus.Delivered;
        await o.T.Db.SaveChangesAsync();
        o.T.Db.ChangeTracker.Clear();

        // Od magacina, ali samo preostale dvije stanice — to nije cijela tura.
        await new OptimizeRouteHandler(o.T.Db, new FiksniOptimizer(9_000)).Handle(new OptimizeRouteCommand(o.Ruta.Id), default);

        Assert.Equal(24_500, (await RutaAsync(o)).KilometrazaMetara);
    }
}
