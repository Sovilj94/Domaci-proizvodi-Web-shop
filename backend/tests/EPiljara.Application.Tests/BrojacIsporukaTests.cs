using EPiljara.Application.Features.Poeni;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Driver;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Popust visi o broju ISPORUČENIH narudžbi, a status Delivered se postavlja na dva mjesta
/// (admin panel i vozačka aplikacija). Oba moraju brojati, i nijedno ne smije brojati dvaput.
/// </summary>
public class BrojacIsporukaTests
{
    private static readonly Microsoft.Extensions.Options.IOptions<EPiljara.Application.Features.Poeni.PoeniOpcije> PoeniOpts =
        Microsoft.Extensions.Options.Options.Create(new PoeniOpcije());

    private sealed class Scena : IDisposable
    {
        private readonly TestDb _t;
        public Infrastructure.Persistence.AppDbContext Ctx => _t.Db;
        public Pretplata Pretplata { get; private set; } = null!;
        public Order Narudzba { get; private set; } = null!;
        public User Vozac { get; private set; } = null!;

        private Scena(TestDb t) => _t = t;

        public static async Task<Scena> SpremiAsync(bool pretplatna = true)
        {
            var s = new Scena(new TestDb());
            var db = s.Ctx;

            var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
            var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            var kat = new Category { Name = "Povrće", Slug = "povrce" };
            var proiz = new Producer { Name = "Marko", Village = "Manjača" };
            var kupac = new User
            {
                Name = "Mujo", Email = "mujo@test.local", PasswordHash = "x", Role = UserRole.Customer,
            };
            var vozacKorisnik = new User
            {
                Name = "Vozač", Email = "vozac@test.local", PasswordHash = "x", Role = UserRole.Driver,
            };
            var korpica = new Product
            {
                Name = "Korpica", Slug = "korpica", Price = 25m, Unit = Unit.Kom, Stock = 10,
                IsAvailable = true, Category = kat, Producer = proiz,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Domacinska,
                Velicina = VelicinaKorpice.Mala,
            };
            db.AddRange(zona, slot, kupac, vozacKorisnik, korpica);
            await db.SaveChangesAsync();

            var pretplata = new Pretplata
            {
                UserId = kupac.Id, ProductId = korpica.Id, Status = StatusPretplate.Aktivna,
                DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id,
                DeliveryAddress = "Testna 1", BrojIsporuka = 2,
            };
            db.Pretplate.Add(pretplata);

            var order = new Order
            {
                Code = "EP-2610-0001", PublicToken = Guid.NewGuid().ToString("N"),
                CustomerId = kupac.Id, CustomerName = "Mujo",
                Status = OrderStatus.OutForDelivery,
                Subtotal = 25m, DeliveryFee = 3m, TotalAmount = 28m,
                DeliverySlotId = slot.Id, DeliveryZoneId = zona.Id,
                DeliveryAddress = "Testna 1", Godina = 2026, Sedmica = 42,
                PretplataId = pretplatna ? pretplata.Id : null,
            };
            db.Orders.Add(order);

            var vozac = new Driver { Name = "Vozač", UserId = vozacKorisnik.Id, Aktivan = true };
            db.Drivers.Add(vozac);
            await db.SaveChangesAsync();

            var ruta = new DeliveryRoute
            {
                DriverId = vozac.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow),
                Naziv = "Ruta 1", DeliveryZoneId = zona.Id,
            };
            ruta.Stops.Add(new RouteStop { OrderId = order.Id, Sequence = 1 });
            db.DeliveryRoutes.Add(ruta);
            await db.SaveChangesAsync();

            s.Pretplata = pretplata;
            s.Narudzba = order;
            s.Vozac = vozacKorisnik;
            return s;
        }

        public async Task<int> BrojIsporukaAsync()
        {
            Ctx.ChangeTracker.Clear();
            var p = await Ctx.Pretplate.AsNoTracking().FirstAsync();
            return p.BrojIsporuka;
        }

        public void Dispose() => _t.Dispose();
    }

    [Fact]
    public async Task Admin_koji_oznaci_isporuceno_dize_brojac()
    {
        using var s = await Scena.SpremiAsync();

        await new UpdateOrderStatusHandler(s.Ctx, PoeniOpts)
            .Handle(new UpdateOrderStatusCommand(s.Narudzba.Id, "Delivered"), default);

        Assert.Equal(3, await s.BrojIsporukaAsync());
    }

    [Fact]
    public async Task Vozac_koji_zatvori_stanicu_dize_brojac()
    {
        using var s = await Scena.SpremiAsync();

        await new UpdateStopStatusHandler(s.Ctx, new FakeCurrentUser(s.Vozac.Id, UserRole.Driver), PoeniOpts)
            .Handle(new UpdateStopStatusCommand(s.Narudzba.Id, "Delivered"), default);

        Assert.Equal(3, await s.BrojIsporukaAsync());
    }

    /// <summary>
    /// Admin koji dvaput klikne isti status ne smije kupcu pokloniti isporuku — popust bi
    /// stigao prerano, a razlika se vidi tek na računu.
    /// </summary>
    [Fact]
    public async Task Ponovno_oznacavanje_isporucenog_ne_broji_drugi_put()
    {
        using var s = await Scena.SpremiAsync();
        var handler = new UpdateOrderStatusHandler(s.Ctx, PoeniOpts);

        await handler.Handle(new UpdateOrderStatusCommand(s.Narudzba.Id, "Delivered"), default);
        await handler.Handle(new UpdateOrderStatusCommand(s.Narudzba.Id, "Delivered"), default);

        Assert.Equal(3, await s.BrojIsporukaAsync());
    }

    [Fact]
    public async Task Neuspjela_dostava_ne_broji()
    {
        using var s = await Scena.SpremiAsync();

        await new UpdateOrderStatusHandler(s.Ctx, PoeniOpts)
            .Handle(new UpdateOrderStatusCommand(s.Narudzba.Id, "FailedDelivery"), default);

        Assert.Equal(2, await s.BrojIsporukaAsync());
    }

    [Fact]
    public async Task Narudzba_bez_pretplate_ne_dira_nijednu()
    {
        using var s = await Scena.SpremiAsync(pretplatna: false);

        await new UpdateOrderStatusHandler(s.Ctx, PoeniOpts)
            .Handle(new UpdateOrderStatusCommand(s.Narudzba.Id, "Delivered"), default);

        Assert.Equal(2, await s.BrojIsporukaAsync());
    }
}
