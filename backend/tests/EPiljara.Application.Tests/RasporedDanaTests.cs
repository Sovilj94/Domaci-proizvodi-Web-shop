using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Dodjela na /admin/dostava i /admin/routes: podjela po lokaciji, redoslijed i redni brojevi.</summary>
public class RasporedDanaTests
{
    private const string Datum = "2026-11-12";

    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public DeliveryZone Borik { get; } = new() { Name = "Borik", IsActive = true };
        public DeliveryZone Laus { get; } = new() { Name = "Lauš", IsActive = true };
        public DeliverySlot Slot { get; } = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        public Driver Vozac { get; } = new() { Name = "Mladen Kovačević", Phone = "065 111 222", Aktivan = true };
        private int _broj;

        public Okruzenje()
        {
            // Magacin zapadno od Borika (sirovi SQL: CHECK magacina traži tačan tekst Id-a).
            T.Db.Database.ExecuteSqlRaw(
                $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', 44.77, 17.15)");
            T.Db.AddRange(Borik, Laus, Slot, Vozac);
            T.Db.SaveChanges();
        }

        public Order Narudzba(double lat, double lon, DeliveryZone? zona = null)
        {
            var o = new Order
            {
                Code = $"EP-T-{++_broj:D4}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac",
                Status = OrderStatus.Confirmed, DeliveryZoneId = (zona ?? Borik).Id, DeliverySlotId = Slot.Id,
                DeliveryAddress = "Adresa", Latitude = lat, Longitude = lon, Godina = 2026, Sedmica = 46,
            };
            T.Db.Orders.Add(o);
            T.Db.SaveChanges();
            return o;
        }

        /// <summary>Zapadni (8, oko 17.200) i istočni (7, oko 17.210) dio Borika.</summary>
        public (List<Order> Zapad, List<Order> Istok) Borik15()
        {
            var z = Enumerable.Range(0, 8).Select(i => Narudzba(44.770 + i * 0.0003, 17.200 + (i % 3) * 0.0003)).ToList();
            var s = Enumerable.Range(0, 7).Select(i => Narudzba(44.770 + i * 0.0003, 17.210 + (i % 3) * 0.0003)).ToList();
            return (z, s);
        }

        public Task Grupno(IEnumerable<Order> n) =>
            new AssignOrdersBulkHandler(T.Db, new NajbliziSusjedOptimizer())
                .Handle(new AssignOrdersBulkCommand(n.Select(o => o.Id).ToList(), Vozac.Id, Datum), default);

        public Task Pojedinacno(Order n) =>
            new AssignOrderDriverHandler(T.Db, new NajbliziSusjedOptimizer())
                .Handle(new AssignOrderDriverCommand(n.Id, Vozac.Id, Datum), default);

        public async Task<List<DeliveryRoute>> RuteAsync()
        {
            T.Db.ChangeTracker.Clear();
            return await T.Db.DeliveryRoutes.AsNoTracking()
                .Include(r => r.Stops).ThenInclude(s => s.Order)
                .OrderBy(r => r.RedniBroj).ToListAsync();
        }

        public void Dispose() => T.Dispose();
    }

    private static bool Zapadna(RouteStop s) => s.Order!.Longitude < 17.205;

    [Fact]
    public async Task Grupna_dodjela_15_Borik_daje_dvije_rute_po_lokaciji()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();

        await o.Grupno(z.Concat(s));

        var rute = await o.RuteAsync();
        Assert.Equal(2, rute.Count);
        Assert.All(rute, r => Assert.True(r.Stops.All(Zapadna) || !r.Stops.Any(Zapadna), "ruta miješa krajeve naselja"));
        Assert.Equal([8, 7], rute.Select(r => r.Stops.Count).OrderByDescending(x => x));
        Assert.Equal(["Borik 1", "Borik 2"], rute.Select(r => r.Naziv).OrderBy(x => x));
        Assert.Equal([1, 2], rute.Select(r => r.RedniBroj!.Value));
        // Magacin je zapadno — zapadna ruta je prva.
        Assert.True(rute[0].Stops.All(Zapadna));
        Assert.All(rute, r => Assert.Equal("vazdusno", r.IzvorRedoslijeda));
    }

    [Fact]
    public async Task Pojedinacna_ide_u_najblizu_rutu()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();
        await o.Grupno(z.Take(6).Concat(s.Take(5)));   // 11 → 6 + 5

        await o.Pojedinacno(o.Narudzba(44.771, 17.2105));  // istok

        var rute = await o.RuteAsync();
        var istocna = rute.Single(r => !r.Stops.Any(Zapadna));
        Assert.Equal(6, istocna.Stops.Count);
        Assert.Equal(6, rute.Single(r => r.Stops.All(Zapadna)).Stops.Count);
    }

    [Fact]
    public async Task Pocela_ruta_se_ne_rastura()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();
        await o.Grupno(z.Concat(s));
        var prije = await o.RuteAsync();
        var zapadna = prije.Single(r => r.Stops.All(Zapadna));
        var isporucena = await o.T.Db.Orders.SingleAsync(x => x.Id == zapadna.Stops.First().OrderId);
        isporucena.Status = OrderStatus.Delivered;
        await o.T.Db.SaveChangesAsync();

        var nove = Enumerable.Range(0, 3).Select(i => o.Narudzba(44.771 + i * 0.0003, 17.2005)).ToList();
        await o.Grupno(nove);

        var rute = await o.RuteAsync();
        var ista = rute.Single(r => r.Id == zapadna.Id);
        Assert.Equal(zapadna.Stops.Select(x => x.OrderId).OrderBy(x => x), ista.Stops.Select(x => x.OrderId).OrderBy(x => x));
        Assert.Equal(1, ista.RedniBroj);
        Assert.All(rute, r => Assert.True(r.Stops.Count <= 10));
        Assert.Equal(18, rute.Sum(r => r.Stops.Count));
    }

    [Fact]
    public async Task Redni_brojevi_pocetih_ostaju()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();
        await o.Grupno(z.Concat(s));
        var druga = (await o.RuteAsync()).Single(r => r.RedniBroj == 2);
        var isporucena = await o.T.Db.Orders.SingleAsync(x => x.Id == druga.Stops.First().OrderId);
        isporucena.Status = OrderStatus.Delivered;
        await o.T.Db.SaveChangesAsync();

        await o.Grupno([o.Narudzba(44.775, 17.135, o.Laus)]);

        var rute = await o.RuteAsync();
        Assert.Equal(2, rute.Single(r => r.Id == druga.Id).RedniBroj);
        // Nepočete idu iza počete (dizajn, odluka 2): počela #2 ostaje, ostale su 3 i 4.
        Assert.Equal([2, 3, 4], rute.Select(r => r.RedniBroj!.Value));
    }

    [Fact]
    public async Task Prva_ruta_je_naselje_najblize_magacinu()
    {
        using var o = new Okruzenje();
        var borik = o.Narudzba(44.770, 17.200);
        var laus = o.Narudzba(44.775, 17.160, o.Laus);  // bliže magacinu (17.15)

        await o.Grupno([borik, laus]);

        var rute = await o.RuteAsync();
        Assert.Equal("Lauš", rute[0].Naziv);
        Assert.Equal("Borik", rute[1].Naziv);
    }

    [Fact]
    public async Task Admin_routes_dijeli_po_lokaciji()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();

        await new AssignRouteHandler(o.T.Db, new NajbliziSusjedOptimizer()).Handle(
            new AssignRouteCommand(Datum, o.Vozac.Id, z.Concat(s).Select(x => x.Id).ToList()), default);

        var rute = await o.RuteAsync();
        Assert.Equal(2, rute.Count);
        Assert.All(rute, r => Assert.True(r.Stops.All(Zapadna) || !r.Stops.Any(Zapadna)));
        Assert.Equal([1, 2], rute.Select(r => r.RedniBroj!.Value));
    }

    [Fact]
    public async Task Podjela_pregled_ne_upisuje()
    {
        using var o = new Okruzenje();
        var (z, s) = o.Borik15();

        var pregled = await new PregledPodjeleHandler(o.T.Db).Handle(
            new PregledPodjeleQuery(z.Concat(s).Select(x => x.Id).ToList(), o.Vozac.Id, Datum), default);

        Assert.Equal([("Borik 1", 8), ("Borik 2", 7)], pregled.Select(p => (p.Naziv, p.Broj)));
        Assert.False(await o.T.Db.DeliveryRoutes.AnyAsync());
    }
}
