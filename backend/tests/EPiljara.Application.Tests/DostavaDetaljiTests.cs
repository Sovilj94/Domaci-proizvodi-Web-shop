using EPiljara.Application.Features.Driver;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Stanica u vozačkoj aplikaciji nosi napomenu kupca i stavke; neuspjela dostava nosi razlog;
/// vozač na današnjoj ruti može ispraviti isporučenu ↔ neuspjelu (13.11.2026).
/// </summary>
public class DostavaDetaljiTests
{
    private static readonly Microsoft.Extensions.Options.IOptions<Features.Poeni.PoeniOpcije> PoeniOpts =
        Microsoft.Extensions.Options.Options.Create(TestOpcije.Poeni());

    private sealed class Scena : IDisposable
    {
        private readonly TestDb _t;
        public Infrastructure.Persistence.AppDbContext Ctx => _t.Db;
        public Order Narudzba { get; private set; } = null!;
        public User Vozac { get; private set; } = null!;
        public Guid PretplataId { get; private set; }

        private Scena(TestDb t) => _t = t;

        public static async Task<Scena> SpremiAsync(int danaUnazad = 0)
        {
            var s = new Scena(new TestDb());
            var db = s.Ctx;

            var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true, Granica = TestNaselje.Granica };
            var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            var kat = new Category { Name = "Povrće", Slug = "povrce" };
            var proiz = new Producer { Name = "Marko", Village = "Manjača" };
            var kupac = new User { Name = "Jelena", Email = "jelena@test.local", PasswordHash = "x", Role = UserRole.Customer };
            var vozacKorisnik = new User { Name = "Vozač", Email = "vozac@test.local", PasswordHash = "x", Role = UserRole.Driver };
            var korpica = new Product
            {
                Name = "Korpica domaćinska", Slug = "korpica", Price = 25m, Unit = Unit.Kom, Stock = 10,
                IsAvailable = true, Category = kat, Producer = proiz,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Domacinska, Velicina = VelicinaKorpice.Mala,
            };
            db.AddRange(zona, slot, kupac, vozacKorisnik, korpica);
            await db.SaveChangesAsync();

            var pretplata = new Pretplata
            {
                UserId = kupac.Id, ProductId = korpica.Id, Status = StatusPretplate.Aktivna,
                DeliveryZoneId = zona.Id, DeliverySlotId = slot.Id, DeliveryAddress = "Kralja Petra 5", BrojIsporuka = 2,
            };
            db.Pretplate.Add(pretplata);

            var order = new Order
            {
                Code = "EP-2611-0001", PublicToken = Guid.NewGuid().ToString("N"),
                CustomerId = kupac.Id, CustomerName = "Jelena", Status = OrderStatus.OutForDelivery,
                Subtotal = 25m, DeliveryFee = 3m, TotalAmount = 28m,
                DeliverySlotId = slot.Id, DeliveryZoneId = zona.Id,
                DeliveryAddress = "Kralja Petra 5", Note = "  Zvono ne radi, zvati telefonom  ",
                Godina = 2026, Sedmica = 46, PretplataId = pretplata.Id,
            };
            order.Items.Add(new OrderItem
            {
                ProductId = korpica.Id, ProductName = "Korpica domaćinska", Unit = Unit.Kom, Quantity = 2, UnitPrice = 12.5m,
            });
            db.Orders.Add(order);

            var vozac = new Driver { Name = "Vozač", UserId = vozacKorisnik.Id, Aktivan = true };
            db.Drivers.Add(vozac);
            await db.SaveChangesAsync();

            var ruta = new DeliveryRoute
            {
                DriverId = vozac.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-danaUnazad),
                Naziv = "Centar", DeliveryZoneId = zona.Id,
            };
            ruta.Stops.Add(new RouteStop { OrderId = order.Id, Sequence = 1 });
            db.DeliveryRoutes.Add(ruta);
            await db.SaveChangesAsync();

            s.Narudzba = order;
            s.Vozac = vozacKorisnik;
            s.PretplataId = pretplata.Id;
            return s;
        }

        public Task<DTOs.OrderDto> ZatvoriAsync(string status, string? razlog = null) =>
            new UpdateStopStatusHandler(Ctx, new FakeCurrentUser(Vozac.Id, UserRole.Driver), PoeniOpts, new FakeEmail())
                .Handle(new UpdateStopStatusCommand(Narudzba.Id, status, razlog), default);

        public async Task<Order> NarudzbaAsync()
        {
            Ctx.ChangeTracker.Clear();
            return await Ctx.Orders.AsNoTracking().SingleAsync();
        }

        public void Dispose() => _t.Dispose();
    }

    [Fact]
    public async Task Stanica_nosi_napomenu_i_stavke()
    {
        using var s = await Scena.SpremiAsync();
        s.Ctx.ChangeTracker.Clear();

        var rute = await new GetDriverRoutesHandler(s.Ctx, new FakeCurrentUser(s.Vozac.Id, UserRole.Driver))
            .Handle(new GetDriverRoutesQuery(null), default);

        var stanica = Assert.Single(Assert.Single(rute).Stops);
        Assert.Equal("Zvono ne radi, zvati telefonom", stanica.Napomena);
        var stavka = Assert.Single(stanica.Stavke);
        Assert.Equal(("Korpica domaćinska", 2m, "Kom"), (stavka.Naziv, stavka.Kolicina, stavka.Jedinica));
    }

    [Fact]
    public async Task Neuspjela_sa_razlogom_upisuje_razlog()
    {
        using var s = await Scena.SpremiAsync();

        var dto = await s.ZatvoriAsync("FailedDelivery", "NijeKodKuce");

        Assert.Equal("NijeKodKuce", dto.RazlogNeuspjeha);
        Assert.Equal(RazlogNeuspjeleDostave.NijeKodKuce, (await s.NarudzbaAsync()).RazlogNeuspjeha);
    }

    [Theory]
    [InlineData("Lijenost")]
    [InlineData("3")]
    public async Task Nepoznat_razlog_se_odbija(string razlog)
    {
        using var s = await Scena.SpremiAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.ZatvoriAsync("FailedDelivery", razlog));
        Assert.Equal("invalid_razlog", ex.Code);
    }

    [Fact]
    public async Task Neuspjela_bez_razloga_radi_za_staru_aplikaciju()
    {
        using var s = await Scena.SpremiAsync();

        await s.ZatvoriAsync("FailedDelivery");

        var o = await s.NarudzbaAsync();
        Assert.Equal((OrderStatus.FailedDelivery, (RazlogNeuspjeleDostave?)null), (o.Status, o.RazlogNeuspjeha));
    }

    [Fact]
    public async Task Isporucena_ispravljena_u_neuspjelu_vraca_brojac_poene_i_brise_racun()
    {
        using var s = await Scena.SpremiAsync();
        await s.ZatvoriAsync("Delivered");
        Assert.True(await s.Ctx.PoeniStavke.AnyAsync());
        Assert.Equal(1, await s.Ctx.FiskalniRacuni.CountAsync());

        await s.ZatvoriAsync("FailedDelivery", "OdbioPrijem");

        var o = await s.NarudzbaAsync();
        Assert.Equal((OrderStatus.FailedDelivery, RazlogNeuspjeleDostave.OdbioPrijem), (o.Status, o.RazlogNeuspjeha));
        Assert.Equal(2, (await s.Ctx.Pretplate.AsNoTracking().SingleAsync()).BrojIsporuka);
        Assert.False(await s.Ctx.PoeniStavke.AnyAsync());
        Assert.False(await s.Ctx.FiskalniRacuni.AnyAsync());
    }

    [Fact]
    public async Task Izdat_racun_se_stornira_a_ponovna_isporuka_dobija_novi()
    {
        using var s = await Scena.SpremiAsync();
        await s.ZatvoriAsync("Delivered");
        var promet = await s.Ctx.FiskalniRacuni.SingleAsync();
        promet.Status = StatusFiskalnogRacuna.Izdat;
        promet.Pokusaja = 1;
        await s.Ctx.SaveChangesAsync();

        await s.ZatvoriAsync("FailedDelivery", "NeJavljaSe");
        s.Ctx.ChangeTracker.Clear();
        var refund = await s.Ctx.FiskalniRacuni.AsNoTracking().SingleAsync(r => r.Vrsta == VrstaFiskalnogRacuna.Storno);
        Assert.Equal((promet.Id, 28m), (refund.RefundiraniRacunId!.Value, refund.Iznos));

        await s.ZatvoriAsync("Delivered");
        s.Ctx.ChangeTracker.Clear();
        Assert.Equal(2, await s.Ctx.FiskalniRacuni.CountAsync(r => r.Vrsta == VrstaFiskalnogRacuna.Promet));
        Assert.Equal(3, (await s.Ctx.Pretplate.AsNoTracking().SingleAsync()).BrojIsporuka);
    }

    [Fact]
    public async Task Neuspjela_ispravljena_u_isporucenu_brise_razlog_i_broji()
    {
        using var s = await Scena.SpremiAsync();
        await s.ZatvoriAsync("FailedDelivery", "PogresnaAdresa");

        await s.ZatvoriAsync("Delivered");

        var o = await s.NarudzbaAsync();
        Assert.Equal((OrderStatus.Delivered, (RazlogNeuspjeleDostave?)null), (o.Status, o.RazlogNeuspjeha));
        Assert.Equal(3, (await s.Ctx.Pretplate.AsNoTracking().SingleAsync()).BrojIsporuka);
        Assert.Equal(1, await s.Ctx.FiskalniRacuni.CountAsync());
    }

    [Fact]
    public async Task Ponovni_dodir_istog_statusa_ne_broji_drugi_put()
    {
        using var s = await Scena.SpremiAsync();
        await s.ZatvoriAsync("Delivered");
        await s.ZatvoriAsync("Delivered");

        Assert.Equal(3, (await s.Ctx.Pretplate.AsNoTracking().SingleAsync()).BrojIsporuka);
        Assert.Equal(1, await s.Ctx.FiskalniRacuni.CountAsync());
    }

    [Fact]
    public async Task Stara_ruta_se_ne_ispravlja_iz_aplikacije()
    {
        using var s = await Scena.SpremiAsync(danaUnazad: 1);
        await s.ZatvoriAsync("Delivered");

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.ZatvoriAsync("FailedDelivery", "NijeKodKuce"));
        Assert.Equal("ispravka_nije_danas", ex.Code);
        Assert.Equal(OrderStatus.Delivered, (await s.NarudzbaAsync()).Status);
    }

    private static Task<IReadOnlyList<RacunStaniceDto>> RacuniAsync(Scena s)
    {
        var dobavljac = new Infrastructure.Services.ProbniFiskalniDobavljac();
        return new GetRacuniStaniceHandler(s.Ctx, new FakeCurrentUser(s.Vozac.Id, UserRole.Driver),
                new Features.Fiskalizacija.ObradiFiskalneRacuneHandler(s.Ctx, dobavljac, new FakeEmail()), dobavljac)
            .Handle(new GetRacuniStaniceQuery(s.Narudzba.Id), default);
    }

    private static async Task PodesiOznakeAsync(Scena s)
    {
        s.Ctx.Podesavanja.Add(new Podesavanja { Id = Podesavanja.JediniId, FiskalOznakaBezPdv = "A", FiskalOznakaPdv = "E" });
        await s.Ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Racun_se_izdaje_odmah_za_stampu_na_vratima()
    {
        using var s = await Scena.SpremiAsync();
        await PodesiOznakeAsync(s);
        await s.ZatvoriAsync("Delivered");

        var racun = Assert.Single(await RacuniAsync(s));

        Assert.Equal(("Promet", "Izdat", 28m), (racun.Vrsta, racun.Status, racun.Iznos));
        Assert.Contains("PROMET - PRODAJA", racun.Zurnal);
        Assert.Contains("Korpica domaćinska", racun.Zurnal);
        Assert.NotNull(racun.QrSadrzaj);
        Assert.True(racun.Probni);
    }

    [Fact]
    public async Task Storniraj_poslije_stampe_daje_storno_racun_bez_podataka_kupca()
    {
        using var s = await Scena.SpremiAsync();
        await PodesiOznakeAsync(s);
        await s.ZatvoriAsync("Delivered");
        await RacuniAsync(s); // odštampan na vratima

        await s.ZatvoriAsync("FailedDelivery", "OdbioPrijem");
        var racuni = await RacuniAsync(s);

        Assert.Equal(["Promet", "Storno"], racuni.Select(r => r.Vrsta));
        var storno = racuni[1];
        Assert.Equal("Izdat", storno.Status);
        Assert.Contains("PROMET - PONISTAVANJE", storno.Zurnal);
        Assert.Contains($"Ref: {racuni[0].Broj}", storno.Zurnal);
    }

    [Fact]
    public async Task Tudja_stanica_nema_racune()
    {
        using var s = await Scena.SpremiAsync();
        var drugi = new User { Name = "Drugi", Email = "drugi@test.local", PasswordHash = "x", Role = UserRole.Driver };
        s.Ctx.Users.Add(drugi);
        s.Ctx.Drivers.Add(new Driver { Name = "Drugi", UserId = drugi.Id, Aktivan = true });
        await s.Ctx.SaveChangesAsync();
        var dobavljac = new Infrastructure.Services.ProbniFiskalniDobavljac();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new GetRacuniStaniceHandler(s.Ctx, new FakeCurrentUser(drugi.Id, UserRole.Driver),
                    new Features.Fiskalizacija.ObradiFiskalneRacuneHandler(s.Ctx, dobavljac, new FakeEmail()), dobavljac)
                .Handle(new GetRacuniStaniceQuery(s.Narudzba.Id), default));
    }
}
