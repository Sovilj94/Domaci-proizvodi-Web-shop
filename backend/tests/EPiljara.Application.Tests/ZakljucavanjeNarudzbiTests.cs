using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Na roku naručivanja sedmice narudžbe se same zaključavaju (ZakljucanaU) — ne otkazuju se i
/// ostaju prihvaćene; poslije toga kupac ih više ne mijenja, a roba ide u nabavku.
/// </summary>
public class ZakljucavanjeNarudzbiTests
{
    // Petak 41/2026 u 16:00, rok 48h ranije = srijeda 41/2026 u 16:00.
    private static readonly DateTime PoslijeRoka = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Friday);
    private static readonly DateTime PrijeRoka = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Monday);

    private sealed record Osnova(TestDb T, Guid ZonaId, Guid SlotId, Product Artikal, User Kupac);

    private static Osnova Napravi()
    {
        var t = new TestDb();
        var slot = new DeliverySlot { DayOfWeek = 5, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var kat = new Category { Name = "Mliječno", Slug = "mlijecno" };
        var proiz = new Producer { Name = "Jelena Petrović", Village = "Šargovac" };
        var sir = new Product
        {
            Name = "Sir", Slug = "sir", Price = 10m, Unit = Unit.Kg, Stock = 5m,
            CategoryId = kat.Id, ProducerId = proiz.Id, Vrsta = VrstaProizvoda.Obican, USpajzu = true,
        };
        var kupac = new User { Name = "Marko Marković", Email = "marko@example.com", Role = UserRole.Customer };
        t.Db.AddRange(slot, zona, kat, proiz, sir, kupac);
        t.Db.SaveChanges();
        return new Osnova(t, zona.Id, slot.Id, sir, kupac);
    }

    private static Order Narudzba(Osnova o, OrderStatus status = OrderStatus.Confirmed, int sedmica = 41)
    {
        var n = new Order
        {
            Code = $"EP-{Guid.NewGuid():N}"[..12], PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = o.Kupac.Id, CustomerName = o.Kupac.Name, Status = status,
            DeliveryZoneId = o.ZonaId, DeliverySlotId = o.SlotId, Godina = 2026, Sedmica = sedmica,
        };
        n.Items.Add(new OrderItem
        {
            OrderId = n.Id, ProductId = o.Artikal.Id, ProductName = o.Artikal.Name, Unit = Unit.Kg,
            Quantity = 2m, UnitPrice = 10m,
        });
        o.T.Db.Orders.Add(n);
        o.T.Db.SaveChanges();
        return n;
    }

    private static Task<IReadOnlyList<SedmicaKljuc>> ZakljucajAsync(Osnova o, DateTime sada) =>
        new ZakljucajNarudzbeHandler(o.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new ZakljucajNarudzbeCommand(sada), default);

    private static async Task<Order> UcitajAsync(Osnova o, Guid id)
    {
        o.T.Db.ChangeTracker.Clear();
        return await o.T.Db.Orders.SingleAsync(x => x.Id == id);
    }

    [Fact]
    public async Task Poslije_roka_narudzba_dobija_ZakljucanaU_i_ostaje_Confirmed()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o);

        await ZakljucajAsync(o, PoslijeRoka);

        var u = await UcitajAsync(o, n.Id);
        Assert.Equal(OrderStatus.Confirmed, u.Status);
        Assert.Equal(PoslijeRoka, u.ZakljucanaU);
    }

    [Fact]
    public async Task Prije_roka_narudzba_ostaje_otvorena()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o);

        var sedmice = await ZakljucajAsync(o, PrijeRoka);

        Assert.Empty(sedmice);
        Assert.Null((await UcitajAsync(o, n.Id)).ZakljucanaU);
    }

    [Fact]
    public async Task Otkazana_se_ne_zakljucava()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Cancelled);

        var sedmice = await ZakljucajAsync(o, PoslijeRoka);

        Assert.Empty(sedmice);
        Assert.Null((await UcitajAsync(o, n.Id)).ZakljucanaU);
    }

    [Fact]
    public async Task Drugi_prolaz_ne_mijenja_ZakljucanaU()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o);

        await ZakljucajAsync(o, PoslijeRoka);
        var drugi = await ZakljucajAsync(o, PoslijeRoka.AddHours(1));

        Assert.Empty(drugi);
        Assert.Equal(PoslijeRoka, (await UcitajAsync(o, n.Id)).ZakljucanaU);
    }

    [Fact]
    public async Task Narudzba_sljedece_sedmice_ostaje_otvorena()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, sedmica: 42);

        await ZakljucajAsync(o, PoslijeRoka);

        Assert.Null((await UcitajAsync(o, n.Id)).ZakljucanaU);
    }

    /// <summary>Stare narudžbe na čekanju (iz vremena odobravanja) postaju prihvaćene.</summary>
    [Fact]
    public async Task Stara_Pending_postaje_Confirmed()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Pending, sedmica: 42);

        await ZakljucajAsync(o, PrijeRoka);

        var u = await UcitajAsync(o, n.Id);
        Assert.Equal(OrderStatus.Confirmed, u.Status);
        Assert.Null(u.ZakljucanaU);
    }

    [Fact]
    public async Task Vraca_zakljucane_sedmice()
    {
        var o = Napravi();
        using var _ = o.T;
        Narudzba(o);
        Narudzba(o);

        var sedmice = await ZakljucajAsync(o, PoslijeRoka);

        Assert.Equal([new SedmicaKljuc(2026, 41)], sedmice);
    }

    /// <summary>
    /// Zaključana narudžba se ne mijenja ni kad sat (u testu) još nije stigao do roka — posao je
    /// odlučio, a editor to poštuje istim kodom greške kao za istekao rok.
    /// </summary>
    [Fact]
    public async Task Editor_odbija_zakljucanu_korpicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        await t.Db.Orders.Where(x => x.Id == dto.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ZakljucanaU, DateTime.UtcNow));
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId]));

        Assert.Equal("rok_istekao", ex.Code);
    }

    [Fact]
    public async Task Zakljucana_korpica_nije_u_listi_za_izmjenu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        Assert.Single(await t.MojeKorpiceAsync());

        await t.Db.Orders.Where(x => x.Id == dto.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ZakljucanaU, DateTime.UtcNow));
        t.Db.ChangeTracker.Clear();

        Assert.Empty(await t.MojeKorpiceAsync());
    }
}
