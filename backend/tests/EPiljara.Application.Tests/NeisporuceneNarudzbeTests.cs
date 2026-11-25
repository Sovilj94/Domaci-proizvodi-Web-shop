using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba kojoj status nije promijenjen do kraja perioda dostave njene sedmice (zadnji termin,
/// npr. petak 20:00) sama postaje neuspjela dostava — admin je poslije može ispraviti.
/// </summary>
public class NeisporuceneNarudzbeTests
{
    private static readonly DateTime Petak = System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Friday);
    private static readonly DateTime PoslijeKrajaDostave = Petak.AddHours(21);
    private static readonly DateTime UTokuDostave = Petak.AddHours(18);

    private sealed record Osnova(TestDb T, Guid ZonaId, Guid SlotId, Product Artikal, User Kupac);

    private static Osnova Napravi()
    {
        var t = new TestDb();
        var cetvrtak = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var petak = new DeliverySlot { DayOfWeek = 5, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var zona = new DeliveryZone { Name = "Centar", DeliveryFee = 3m };
        var kat = new Category { Name = "Mliječno", Slug = "mlijecno" };
        var proiz = new Producer { Name = "Jelena", Village = "Šargovac" };
        var sir = new Product
        {
            Name = "Sir", Slug = "sir", Price = 10m, Unit = Unit.Kg, Stock = 5m,
            CategoryId = kat.Id, ProducerId = proiz.Id, Vrsta = VrstaProizvoda.Obican, USpajzu = true,
        };
        var kupac = new User { Name = "Marko", Email = "marko@example.com", Role = UserRole.Customer };
        t.Db.AddRange(cetvrtak, petak, zona, kat, proiz, sir, kupac);
        t.Db.SaveChanges();
        return new Osnova(t, zona.Id, cetvrtak.Id, sir, kupac);
    }

    private static Order Narudzba(Osnova o, OrderStatus status, int sedmica = 41)
    {
        var n = new Order
        {
            Code = $"EP-{Guid.NewGuid():N}"[..12], PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = o.Kupac.Id, CustomerName = o.Kupac.Name, Status = status,
            DeliveryZoneId = o.ZonaId, DeliverySlotId = o.SlotId, Godina = 2026, Sedmica = sedmica,
        };
        o.T.Db.Orders.Add(n);
        o.T.Db.SaveChanges();
        return n;
    }

    private static Task<int> OznaciAsync(Osnova o, DateTime sada) =>
        new OznaciNeisporuceneHandler(o.T.Db).Handle(new OznaciNeisporuceneCommand(sada), default);

    private static async Task<OrderStatus> StatusAsync(Osnova o, Guid id)
    {
        o.T.Db.ChangeTracker.Clear();
        return (await o.T.Db.Orders.SingleAsync(x => x.Id == id)).Status;
    }

    [Theory]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Packing)]
    [InlineData(OrderStatus.OutForDelivery)]
    [InlineData(OrderStatus.Pending)]
    public async Task Poslije_kraja_dostave_neizmijenjena_postaje_neuspjela(OrderStatus status)
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, status);

        Assert.Equal(1, await OznaciAsync(o, PoslijeKrajaDostave));

        Assert.Equal(OrderStatus.FailedDelivery, await StatusAsync(o, n.Id));
    }

    /// <summary>Kraj dostave je ZADNJI termin sedmice (petak), ne prvi (četvrtak).</summary>
    [Fact]
    public async Task Dok_traje_zadnji_termin_nista_se_ne_mijenja()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.OutForDelivery);

        Assert.Equal(0, await OznaciAsync(o, UTokuDostave));

        Assert.Equal(OrderStatus.OutForDelivery, await StatusAsync(o, n.Id));
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.FailedDelivery)]
    public async Task Zavrsene_se_ne_diraju(OrderStatus status)
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, status);

        Assert.Equal(0, await OznaciAsync(o, PoslijeKrajaDostave));

        Assert.Equal(status, await StatusAsync(o, n.Id));
    }

    [Fact]
    public async Task Sljedeca_sedmica_se_ne_dira()
    {
        var o = Napravi();
        using var _ = o.T;
        var n = Narudzba(o, OrderStatus.Confirmed, sedmica: 42);

        await OznaciAsync(o, PoslijeKrajaDostave);

        Assert.Equal(OrderStatus.Confirmed, await StatusAsync(o, n.Id));
    }

    /// <summary>Sedmice za ekran: koja se pakuje (posljednja zaključana) i koja je otvorena.</summary>
    [Fact]
    public async Task Sedmice_rada_pakovanje_je_sedmica_prije_otvorene()
    {
        var o = Napravi();
        using var _ = o.T;

        // Srijeda 41 u 18h: rok sedmice 41 (uto 16h za čet.) je prošao → otvorena je 42, pakuje se 41.
        var s = await new GetSedmiceRadaHandler(o.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetSedmiceRadaQuery(System.Globalization.ISOWeek.ToDateTime(2026, 41, DayOfWeek.Wednesday).AddHours(18)), default);

        Assert.Equal((2026, 41), (s.Pakovanje.Godina, s.Pakovanje.Sedmica));
        Assert.Equal((2026, 42), (s.Otvorena.Godina, s.Otvorena.Sedmica));
    }
}
