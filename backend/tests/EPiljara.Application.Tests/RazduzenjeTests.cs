using EPiljara.Application.Features.Voznje;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Naplata je pouzećem: vozač donosi gotovinu za svaku isporučenu narudžbu. Admin vidi koliko ko treba
/// da preda po danu, evidentira predaju, i vidi ko još nije razdužen.
/// </summary>
public class RazduzenjeTests
{
    private static readonly DateOnly Danas = DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public User KorisnikMladen { get; } = new() { Name = "Mladen", Email = "mladen@example.com", Role = UserRole.Driver };
        public Driver Mladen { get; }
        public Driver Dragan { get; }
        private readonly DeliveryZone _zona = new() { Name = "Centar" };
        private readonly DeliverySlot _slot = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        private int _broj;

        public Okruzenje()
        {
            Mladen = new Driver { Name = "Mladen Kovačević", Phone = "065 111 222", UserId = KorisnikMladen.Id };
            Dragan = new Driver { Name = "Dragan Petrović", Phone = "065 333 444" };
            T.Db.AddRange(KorisnikMladen, Mladen, Dragan, _zona, _slot);
            T.Db.SaveChanges();
        }

        /// <summary>Jedna ruta vozača za dan, sa narudžbama datih iznosa i statusa.</summary>
        public void Ruta(Driver vozac, DateOnly datum, params (decimal Iznos, OrderStatus Status)[] narudzbe)
        {
            var ruta = new DeliveryRoute { Date = datum, DriverId = vozac.Id, Naziv = "Centar", DeliveryZoneId = _zona.Id };
            T.Db.Add(ruta);
            var i = 0;
            foreach (var (iznos, status) in narudzbe)
            {
                var n = new Order
                {
                    Code = $"EP-R-{++_broj:D4}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "Kupac",
                    Status = status, DeliveryZoneId = _zona.Id, DeliverySlotId = _slot.Id, Godina = 2026, Sedmica = 46,
                    DeliveryAddress = "Ulica 1", TotalAmount = iznos,
                };
                T.Db.Add(n);
                T.Db.RouteStops.Add(new RouteStop { RouteId = ruta.Id, OrderId = n.Id, Sequence = ++i });
            }
            T.Db.SaveChanges();
            T.Db.ChangeTracker.Clear();
        }

        public Task<RazduzenjeDto> PregledAsync(DateOnly od, DateOnly doUklj)
        {
            T.Db.ChangeTracker.Clear();
            return new GetRazduzenjeHandler(T.Db).Handle(new GetRazduzenjeQuery(od, doUklj), default);
        }

        public Task<RazduzenjeDanDto> PredajaAsync(Driver vozac, DateOnly datum, decimal? iznos)
        {
            T.Db.ChangeTracker.Clear();
            return new EvidentirajPredajuHandler(T.Db).Handle(new EvidentirajPredajuCommand(vozac.Id, datum, iznos), default);
        }

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Za_predati_je_zbir_samo_isporucenih_narudzbi_vozaca_za_dan()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (24.50m, OrderStatus.Delivered), (30m, OrderStatus.Delivered),
            (15m, OrderStatus.FailedDelivery), (12m, OrderStatus.Confirmed));
        o.Ruta(o.Mladen, Danas, (10m, OrderStatus.Delivered)); // druga ruta istog dana
        o.Ruta(o.Dragan, Danas, (40m, OrderStatus.Delivered));

        var p = await o.PregledAsync(Danas, Danas);

        var mladen = p.Dani.Single(d => d.DriverId == o.Mladen.Id);
        Assert.Equal(64.50m, mladen.ZaPredati);
        Assert.Equal(3, mladen.Dostavljeno);
        Assert.Null(mladen.Predano);
        Assert.Equal(40m, p.Dani.Single(d => d.DriverId == o.Dragan.Id).ZaPredati);
    }

    [Fact]
    public async Task Dan_bez_ijedne_isporuke_se_ne_prikazuje()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (15m, OrderStatus.FailedDelivery), (12m, OrderStatus.Confirmed));

        var p = await o.PregledAsync(Danas, Danas);

        Assert.Empty(p.Dani);
        Assert.Empty(p.Nerazduzeno);
    }

    [Fact]
    public async Task Predaja_se_pamti_i_racuna_razliku()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (50m, OrderStatus.Delivered));

        var dan = await o.PredajaAsync(o.Mladen, Danas, 45m);

        Assert.Equal(45m, dan.Predano);
        Assert.Equal(-5m, dan.Razlika);
        Assert.NotNull(dan.PredanoU);
        var p = await o.PregledAsync(Danas, Danas);
        Assert.Equal(45m, p.Dani.Single().Predano);
    }

    [Fact]
    public async Task Predaja_bez_iznosa_ponistava_raniju()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (50m, OrderStatus.Delivered));
        await o.PredajaAsync(o.Mladen, Danas, 50m);

        var dan = await o.PredajaAsync(o.Mladen, Danas, null);

        Assert.Null(dan.Predano);
        Assert.Null(dan.PredanoU);
        Assert.Single((await o.PregledAsync(Danas, Danas)).Nerazduzeno);
    }

    [Fact]
    public async Task Predaja_ne_dira_kilometrazu_i_gorivo_tog_dana()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (50m, OrderStatus.Delivered));
        o.T.Db.DnevneVoznje.Add(new DnevnaVoznja { DriverId = o.Mladen.Id, Datum = Danas, KmPocetak = 100, KmKraj = 180, PlacenoGorivo = 30m });
        o.T.Db.SaveChanges();

        await o.PredajaAsync(o.Mladen, Danas, 50m);

        var v = await o.T.Db.DnevneVoznje.AsNoTracking().SingleAsync();
        Assert.Equal(80, v.PredjenoKm);
        Assert.Equal(30m, v.PlacenoGorivo);
        Assert.Equal(50m, v.PredanoGotovine);
    }

    [Fact]
    public async Task Nerazduzeno_sabira_sve_nepredane_dane_po_vozacu_i_van_odabranog_perioda()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas.AddDays(-3), (20m, OrderStatus.Delivered));
        o.Ruta(o.Mladen, Danas.AddDays(-1), (30m, OrderStatus.Delivered));
        o.Ruta(o.Mladen, Danas, (40m, OrderStatus.Delivered));
        o.Ruta(o.Dragan, Danas, (25m, OrderStatus.Delivered));
        await o.PredajaAsync(o.Mladen, Danas.AddDays(-1), 30m);
        await o.PredajaAsync(o.Dragan, Danas, 25m);

        var p = await o.PregledAsync(Danas, Danas);

        var mladen = Assert.Single(p.Nerazduzeno);
        Assert.Equal(o.Mladen.Id, mladen.DriverId);
        Assert.Equal(60m, mladen.Iznos);
        Assert.Equal(2, mladen.Dana);
        Assert.Equal(Danas.AddDays(-3), mladen.OdDatuma);
    }

    [Fact]
    public async Task Predao_sve_razduzuje_sve_nepredane_dane_vozaca_do_datuma_u_punom_iznosu()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas.AddDays(-3), (20m, OrderStatus.Delivered));
        o.Ruta(o.Mladen, Danas.AddDays(-1), (30m, OrderStatus.Delivered));
        o.Ruta(o.Mladen, Danas, (40m, OrderStatus.Delivered));
        o.Ruta(o.Dragan, Danas.AddDays(-1), (25m, OrderStatus.Delivered));
        await o.PredajaAsync(o.Mladen, Danas.AddDays(-1), 28m); // već evidentirano sa manjkom — ostaje

        var broj = await new RazduziSveHandler(o.T.Db)
            .Handle(new RazduziSveCommand(o.Mladen.Id, Danas.AddDays(-1)), default);

        Assert.Equal(1, broj);
        var p = await o.PregledAsync(Danas.AddDays(-3), Danas);
        var mladen = p.Dani.Where(d => d.DriverId == o.Mladen.Id).ToDictionary(d => d.Datum);
        Assert.Equal(20m, mladen[Danas.AddDays(-3)].Predano);
        Assert.Equal(28m, mladen[Danas.AddDays(-1)].Predano);
        Assert.Null(mladen[Danas].Predano);
        Assert.Contains(p.Nerazduzeno, n => n.DriverId == o.Dragan.Id);
    }

    [Fact]
    public async Task Predaja_ne_moze_biti_negativna_ni_za_nepostojeceg_vozaca()
    {
        using var o = new Okruzenje();
        o.Ruta(o.Mladen, Danas, (50m, OrderStatus.Delivered));

        Assert.False(new EvidentirajPredajuValidator().Validate(new EvidentirajPredajuCommand(o.Mladen.Id, Danas, -1m)).IsValid);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new EvidentirajPredajuHandler(o.T.Db).Handle(new EvidentirajPredajuCommand(Guid.NewGuid(), Danas, 10m), default));
    }

    [Fact]
    public async Task Vozac_vidi_svoju_statistiku_mjeseca_i_gotovinu_kod_sebe()
    {
        using var o = new Okruzenje();
        var pocetakMjeseca = new DateOnly(Danas.Year, Danas.Month, 1);
        o.Ruta(o.Mladen, pocetakMjeseca.AddDays(-1), (99m, OrderStatus.Delivered)); // prošli mjesec, nepredano
        o.Ruta(o.Mladen, Danas, (20m, OrderStatus.Delivered), (30m, OrderStatus.Delivered), (5m, OrderStatus.FailedDelivery));
        o.Ruta(o.Dragan, Danas, (70m, OrderStatus.Delivered));
        o.T.Db.DnevneVoznje.Add(new DnevnaVoznja { DriverId = o.Mladen.Id, Datum = Danas, KmPocetak = 1000, KmKraj = 1087 });
        o.T.Db.SaveChanges();

        var s = await new GetMojaStatistikaHandler(o.T.Db, new FakeCurrentUser(o.KorisnikMladen.Id, UserRole.Driver))
            .Handle(new GetMojaStatistikaQuery(), default);

        Assert.Equal(2, s.DostavljenoMjesec);
        Assert.Equal(1, s.NeuspjeloMjesec);
        Assert.Equal(87, s.PredjenoKmMjesec);
        Assert.Equal(2, s.DostavljenoDanas);
        Assert.Equal(50m, s.GotovinaDanas);
        Assert.Equal(149m, s.Nerazduzeno);
    }
}
