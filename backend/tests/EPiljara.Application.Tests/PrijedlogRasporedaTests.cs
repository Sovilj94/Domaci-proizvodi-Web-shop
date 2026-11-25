using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Prijedlog raspodjele za dan: uzima nedodijeljene narudžbe sedmice koja se pakuje čiji je
/// termin baš taj dan, grupiše ih po naselju i dijeli na aktivne vozače (vidi RasporedPoKombijima).
/// </summary>
public class PrijedlogRasporedaTests
{
    private static readonly DateTime Cetvrtak = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Thursday);

    [Fact]
    public async Task Prijedlog_uzima_samo_narudzbe_tog_dana_i_sedmice_po_naseljima()
    {
        using var t = new TestDb();
        // CHECK CK_Magacin_JedanRed poredi tekst Id-ja — EF ga u SQLite piše drugačije, pa SQL kao u KilometrazaRuteTests.
#pragma warning disable EF1002
        await t.Db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"Magacin\" (\"Id\", \"Naziv\", \"Latitude\", \"Longitude\") VALUES ('{Magacin.JediniId}', 'Magacin', 44.77, 17.19)");
#pragma warning restore EF1002
        var cet = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var pet = new DeliverySlot { DayOfWeek = 5, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var istok = new DeliveryZone { Name = "Istok", CentarLat = 44.77, CentarLon = 17.25 };
        var zapad = new DeliveryZone { Name = "Zapad", CentarLat = 44.77, CentarLon = 17.13 };
        var ana = new Driver { Name = "Ana", Aktivan = true };
        var bojan = new Driver { Name = "Bojan", Aktivan = true };
        var neaktivan = new Driver { Name = "Ceca", Aktivan = false };
        t.Db.AddRange(cet, pet, istok, zapad, ana, bojan, neaktivan);

        Order N(DeliveryZone z, DeliverySlot s, int sedmica = 41, OrderStatus status = OrderStatus.Confirmed) => new()
        {
            Code = $"EP-{Guid.NewGuid():N}"[..12], PublicToken = Guid.NewGuid().ToString("N"), CustomerName = "K",
            Status = status, DeliveryZoneId = z.Id, DeliverySlotId = s.Id, Godina = 2026, Sedmica = sedmica,
        };
        for (var i = 0; i < 20; i++) t.Db.Orders.Add(N(istok, cet));
        for (var i = 0; i < 20; i++) t.Db.Orders.Add(N(zapad, cet));
        t.Db.Orders.Add(N(istok, pet));                                   // petak — ne ide u četvrtak
        t.Db.Orders.Add(N(istok, cet, sedmica: 42));                      // druga sedmica
        t.Db.Orders.Add(N(zapad, cet, status: OrderStatus.Cancelled));    // otkazana
        await t.Db.SaveChangesAsync();

        var p = await new PredloziRasporedHandler(t.Db)
            .Handle(new PredloziRasporedQuery(Cetvrtak.ToString("yyyy-MM-dd"), 2026, 41), default);

        Assert.Equal(2, p.Kombiji.Count);
        Assert.Contains(p.Kombiji, k => k.Naselja.Single().Naziv == "Istok" && k.Naselja.Single().OrderIds.Count == 20);
        Assert.Contains(p.Kombiji, k => k.Naselja.Single().Naziv == "Zapad" && k.Naselja.Single().OrderIds.Count == 20);
        Assert.DoesNotContain(p.Kombiji, k => k.VozacId == neaktivan.Id);
        Assert.Empty(p.Nerasporedjeno);
    }
}
