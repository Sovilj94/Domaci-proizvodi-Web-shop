using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Voznje;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Dnevna tura vozača: jedan obilazak magacin → sve stanice dana (sve rute/zone) → magacin, procjena goriva
/// i vozačev unos sa pumpe.
/// </summary>
public class VoznjeTests
{
    private sealed class BrojaciOptimizer(double? metara) : IRouteOptimizer
    {
        public List<(double Lat, double Lon, int Stanica)> Pozivi { get; } = [];

        public Task<RouteOptimizationResult> OptimizeAsync(double startLat, double startLon,
            IReadOnlyList<RouteStopInput> stops, CancellationToken cancellationToken = default)
        {
            Pozivi.Add((startLat, startLon, stops.Count));
            return Task.FromResult(new RouteOptimizationResult(stops.Select(s => s.OrderId).ToList(), metara, metara is null ? null : 3600, null));
        }
    }

    private const double MagLat = 44.577975, MagLon = 17.005162;
    private static readonly DateOnly Danas = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed record Osnova(TestDb T, Driver Vozac, User Korisnik, DeliveryZone Zona, DeliverySlot Slot);

    private static Osnova Napravi()
    {
        var t = new TestDb();
        t.Db.Database.ExecuteSqlRaw(
            $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', {MagLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {MagLon.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var korisnik = new User { Name = "Mladen Kovačević", Email = "mladen@example.com", Role = UserRole.Driver };
        var vozac = new Driver { Name = "Mladen Kovačević", Phone = "065 111 222", UserId = korisnik.Id };
        t.Db.AddRange(slot, zona, korisnik, vozac);
        t.Db.CijeneGoriva.Add(new CijenaGoriva { VaziOd = new DateOnly(2020, 1, 1), CijenaPoLitru = 2.50m, PotrosnjaL100km = 8m });
        t.Db.SaveChanges();
        return new Osnova(t, vozac, korisnik, zona, slot);
    }

    /// <summary>Ruta sa stanicama; koordinate null = narudžba bez lokacije.</summary>
    private static List<Order> Ruta(Osnova o, DateOnly datum, string naziv, params (double? Lat, double? Lon, OrderStatus Status)[] stanice)
    {
        var ruta = new DeliveryRoute { Date = datum, DriverId = o.Vozac.Id, Naziv = naziv, DeliveryZoneId = o.Zona.Id };
        o.T.Db.DeliveryRoutes.Add(ruta);
        var narudzbe = new List<Order>();
        for (var i = 0; i < stanice.Length; i++)
        {
            var n = new Order
            {
                Code = $"EP-{naziv}-{i}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac",
                Status = stanice[i].Status, DeliveryZoneId = o.Zona.Id, DeliverySlotId = o.Slot.Id, Godina = 2026, Sedmica = 45,
                Latitude = stanice[i].Lat, Longitude = stanice[i].Lon, DeliveryAddress = "Ulica 1",
            };
            o.T.Db.Orders.Add(n);
            o.T.Db.RouteStops.Add(new RouteStop { RouteId = ruta.Id, OrderId = n.Id, Sequence = i + 1 });
            narudzbe.Add(n);
        }
        o.T.Db.SaveChanges();
        o.T.Db.ChangeTracker.Clear();
        return narudzbe;
    }

    private static async Task<IReadOnlyList<VoznjaDto>> DanAsync(Osnova o, IRouteOptimizer opt, DateOnly? datum = null)
    {
        o.T.Db.ChangeTracker.Clear();
        return await new GetVoznjeDanaHandler(o.T.Db, opt).Handle(new GetVoznjeDanaQuery(datum ?? Danas), default);
    }

    [Fact]
    public async Task Sve_rute_dana_su_jedna_tura_od_magacina_i_nazad()
    {
        var o = Napravi();
        using var _ = o.T;
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Packing), (44.771, 17.19, OrderStatus.Packing));
        Ruta(o, Danas, "Borik", (44.78, 17.20, OrderStatus.Packing));
        var opt = new BrojaciOptimizer(62_400);

        var v = Assert.Single(await DanAsync(o, opt));

        var poziv = Assert.Single(opt.Pozivi);
        Assert.Equal((MagLat, MagLon, 3), poziv);
        Assert.Equal(62.4, v.Km);
        Assert.Equal("osrm", v.Izvor);
        Assert.Equal(3, v.Stanica);
        // 62,4 km × 8 l/100 km × 2,50 KM/l
        Assert.Equal(5.0m, v.ProcjenaLitara);
        Assert.Equal(12.48m, v.ProcjenaGoriva);
    }

    [Fact]
    public async Task Bez_osrm_procjena_je_vazdusna_linija_puta_faktor()
    {
        var o = Napravi();
        using var _ = o.T;
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Packing));

        var v = Assert.Single(await DanAsync(o, new BrojaciOptimizer(null)));

        var tamoNazad = 2 * DnevnaVoznjaProcjena.Haversine(MagLat, MagLon, 44.77, 17.19);
        Assert.Equal("vazdusno", v.Izvor);
        Assert.Equal(Math.Round(Math.Round(tamoNazad * DnevnaVoznjaProcjena.FaktorPuta) / 1000, 1), v.Km);
    }

    [Fact]
    public async Task Procjena_se_preracunava_samo_kad_se_promijene_stanice()
    {
        var o = Napravi();
        using var _ = o.T;
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Packing));
        var opt = new BrojaciOptimizer(40_000);

        await DanAsync(o, opt);
        await DanAsync(o, opt);
        Assert.Single(opt.Pozivi);

        Ruta(o, Danas, "Borik", (44.78, 17.20, OrderStatus.Packing));
        await DanAsync(o, opt);
        Assert.Equal(2, opt.Pozivi.Count);
        Assert.Equal(2, opt.Pozivi[1].Stanica);
    }

    [Fact]
    public async Task Otkazane_i_stanice_bez_koordinata_ne_ulaze_u_turu()
    {
        var o = Napravi();
        using var _ = o.T;
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Packing), (44.771, 17.19, OrderStatus.Cancelled), (null, null, OrderStatus.Packing));
        var opt = new BrojaciOptimizer(30_000);

        var v = Assert.Single(await DanAsync(o, opt));

        Assert.Equal(1, opt.Pozivi.Single().Stanica);
        Assert.Equal(2, v.Stanica);
        Assert.Equal(1, v.BezKoordinata);
    }

    [Fact]
    public async Task Vozac_unosi_gorivo_i_vidi_odstupanje()
    {
        var o = Napravi();
        using var _ = o.T;
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Delivered));
        var opt = new BrojaciOptimizer(50_000); // 50 km → 4 l → 10,00 KM
        var vozac = new FakeCurrentUser(o.Korisnik.Id, UserRole.Driver);

        var v = await new SacuvajMojeGorivoHandler(o.T.Db, vozac, opt, new FakeEmail())
            .Handle(new SacuvajMojeGorivoCommand(Danas, 13m, 5.2m, "  Gužva na Kozari "), default);

        Assert.Equal(10.00m, v.ProcjenaGoriva);
        Assert.Equal(13m, v.PlacenoGorivo);
        Assert.Equal(5.2m, v.Litara);
        Assert.Equal("Gužva na Kozari", v.Napomena);
        Assert.Equal("vozac", v.PlacenoUnio);
        Assert.Equal(30m, v.OdstupanjeProcenat);
        Assert.True(v.BitnoOdstupanje);

        o.T.Db.ChangeTracker.Clear();
        var obrisano = await new SacuvajMojeGorivoHandler(o.T.Db, vozac, opt, new FakeEmail())
            .Handle(new SacuvajMojeGorivoCommand(Danas, null, null, null), default);
        Assert.Null(obrisano.PlacenoGorivo);
        Assert.Null(obrisano.OdstupanjeProcenat);
    }

    [Fact]
    public async Task Vozac_ne_unosi_gorivo_za_dan_bez_dostava_ni_za_star_dan()
    {
        var o = Napravi();
        using var _ = o.T;
        var stari = Danas.AddDays(-(SacuvajMojeGorivoHandler.DanaUnazad + 1));
        Ruta(o, stari, "Centar", (44.77, 17.19, OrderStatus.Delivered));
        var h = new SacuvajMojeGorivoHandler(o.T.Db, new FakeCurrentUser(o.Korisnik.Id, UserRole.Driver), new BrojaciOptimizer(1), new FakeEmail());

        var nema = await Assert.ThrowsAsync<DomainException>(() => h.Handle(new SacuvajMojeGorivoCommand(Danas, 10m, null, null), default));
        Assert.Equal("nema_voznje", nema.Code);
        var star = await Assert.ThrowsAsync<DomainException>(() => h.Handle(new SacuvajMojeGorivoCommand(stari, 10m, null, null), default));
        Assert.Equal("datum_prestar", star.Code);
    }

    [Fact]
    public async Task Unos_goriva_prezivi_skidanje_svih_stanica_a_prazan_dan_bez_unosa_nestaje()
    {
        var o = Napravi();
        using var _ = o.T;
        var sutra = Danas.AddDays(1);
        Ruta(o, Danas, "Centar", (44.77, 17.19, OrderStatus.Delivered));
        Ruta(o, sutra, "Borik", (44.78, 17.20, OrderStatus.Packing));
        var opt = new BrojaciOptimizer(10_000);
        await new SacuvajMojeGorivoHandler(o.T.Db, new FakeCurrentUser(o.Korisnik.Id, UserRole.Driver), opt, new FakeEmail())
            .Handle(new SacuvajMojeGorivoCommand(Danas, 5m, null, null), default);
        await DanAsync(o, opt, sutra);

        o.T.Db.RouteStops.RemoveRange(o.T.Db.RouteStops);
        await o.T.Db.SaveChangesAsync();
        await DanAsync(o, opt, Danas);
        await DanAsync(o, opt, sutra);

        o.T.Db.ChangeTracker.Clear();
        var ostale = await o.T.Db.DnevneVoznje.ToListAsync();
        var v = Assert.Single(ostale);
        Assert.Equal(Danas, v.Datum);
        Assert.Equal(5m, v.PlacenoGorivo);
    }
}
