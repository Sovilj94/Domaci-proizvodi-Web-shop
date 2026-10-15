using EPiljara.Application.Features.Orders;
using EPiljara.Application.Features.PromoKodovi;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>Provjera koda na checkoutu (P6b, Task 2).</summary>
public class ProvjeraPromoKodaTests
{
    private sealed class Okruzenje : IDisposable
    {
        public TestDb T { get; } = new();
        public Guid ProizvodId { get; private set; }
        public DeliveryZone Zona { get; } = new() { Name = "Centar", DeliveryFee = 3m, IsActive = true };
        public DeliverySlot Slot { get; } = new() { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        private int _brojac;

        public Okruzenje()
        {
            var kat = new Category { Name = "Med", Slug = "med" };
            var pro = new Producer { Name = "Porodica", Village = "Han" };
            T.Db.Categories.Add(kat);
            T.Db.Producers.Add(pro);
            T.Db.DeliveryZones.Add(Zona);
            T.Db.DeliverySlots.Add(Slot);
            T.Db.SaveChanges();
            var p = new Product
            {
                Name = "Med", Slug = "med", Price = 20m, Stock = 100, IsAvailable = true,
                CategoryId = kat.Id, ProducerId = pro.Id,
            };
            T.Db.Products.Add(p);
            T.Db.SaveChanges();
            ProizvodId = p.Id;
        }

        public PromoKod Kod(Action<PromoKod>? podesi = null)
        {
            var k = new PromoKod { Kod = "JESEN5", Opis = "Test", Tip = TipPopusta.Procenat, Vrijednost = 10m };
            podesi?.Invoke(k);
            T.Db.PromoKodovi.Add(k);
            T.Db.SaveChanges();
            return k;
        }

        public void Narudzba(Guid? customerId = null, string? email = null)
        {
            _brojac++;
            T.Db.Orders.Add(new Order
            {
                Code = $"EP-{_brojac:0000}", PublicToken = Guid.NewGuid().ToString("N"),
                CustomerId = customerId, CustomerEmail = email, CustomerName = "Test",
                Status = OrderStatus.Pending, DeliveryAddress = "Testna 1",
                DeliverySlotId = Slot.Id, DeliveryZoneId = Zona.Id,
            });
            T.Db.SaveChanges();
        }

        public Task<EPiljara.Application.DTOs.ProvjeraPromoKodaDto> Provjeri(
            Guid? prijavljen, string kod = "jesen5", string? email = null, decimal kolicina = 2m) =>
            new ProvjeriPromoKodHandler(T.Db, new FakeCurrentUser(prijavljen)).Handle(
                new ProvjeriPromoKodQuery(kod, email, [new OrderItemInput(ProizvodId, kolicina)]), default);

        public void Dispose() => T.Dispose();
    }

    [Fact]
    public async Task Subtotal_se_racuna_iz_cijene_u_bazi_i_popust_po_pravilu()
    {
        using var o = new Okruzenje();
        o.Kod();

        var rez = await o.Provjeri(null);

        Assert.Equal(40m, rez.Subtotal);
        Assert.Equal(4m, rez.Popust);
        Assert.Equal("JESEN5", rez.Kod);
    }

    [Fact]
    public async Task Nedostupan_proizvod_baca_product_unavailable()
    {
        using var o = new Okruzenje();
        o.Kod();
        o.T.Db.Products.Single().IsAvailable = false;
        o.T.Db.SaveChanges();

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.Provjeri(null));

        Assert.Equal("product_unavailable", ex.Code);
    }

    [Fact]
    public async Task Prijavljen_kupac_se_prepoznaje_i_po_emailu_ranije_narudzbe()
    {
        using var o = new Okruzenje();
        o.Kod(k => k.SamoNoviKupci = true);
        var kupac = Guid.NewGuid();

        // Narudžba vezana samo za e-mail prijavljenog (npr. nastala kao gost), pa po id-u ne bi bila nađena.
        o.Narudzba(email: new FakeCurrentUser(kupac).Email);
        var ex = await Assert.ThrowsAsync<DomainException>(() => o.Provjeri(kupac));
        Assert.Equal(PromoKodPravila.Greska, ex.Code);
    }

    [Fact]
    public async Task Prijavljen_kupac_sa_ranijom_narudzbom_po_id_je_odbijen()
    {
        using var o = new Okruzenje();
        o.Kod(k => k.SamoNoviKupci = true);
        var kupac = Guid.NewGuid();
        o.T.Db.Users.Add(new User { Id = kupac, Email = "k@test.local", Name = "K", PasswordHash = "x" });
        o.T.Db.SaveChanges();
        o.Narudzba(customerId: kupac);

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.Provjeri(kupac));

        Assert.Equal(PromoKodPravila.Greska, ex.Code);
    }

    [Fact]
    public async Task Gost_sa_emailom_ranije_narudzbe_je_odbijen()
    {
        using var o = new Okruzenje();
        o.Kod(k => k.SamoNoviKupci = true);
        o.Narudzba(email: "gost@test.local");

        var ex = await Assert.ThrowsAsync<DomainException>(() => o.Provjeri(null, email: "Gost@Test.local"));

        Assert.Equal(PromoKodPravila.Greska, ex.Code);
    }

    [Fact]
    public void Validator_trazi_kod_i_stavke()
    {
        var r = new ProvjeriPromoKodValidator().Validate(new ProvjeriPromoKodQuery("", null, []));

        var poruke = r.Errors.Select(e => e.ErrorMessage).ToArray();
        Assert.Contains("Unesite promo kod.", poruke);
        Assert.Contains("Korpa je prazna.", poruke);
    }

    [Fact]
    public void Validator_odbija_nula_kolicinu_i_null_stavku()
    {
        var id = Guid.NewGuid();
        var nula = new ProvjeriPromoKodValidator().Validate(
            new ProvjeriPromoKodQuery("JESEN5", null, [new OrderItemInput(id, 0m)]));
        var nul = new ProvjeriPromoKodValidator().Validate(
            new ProvjeriPromoKodQuery("JESEN5", null, [null!]));

        Assert.Equal("Neispravna količina.", Assert.Single(nula.Errors).ErrorMessage);
        Assert.Equal("Neispravna količina.", Assert.Single(nul.Errors).ErrorMessage);
    }

    [Fact]
    public void Validator_odbija_previse_stavki_i_null_stavke()
    {
        var stavke = Enumerable.Range(0, 101).Select(_ => new OrderItemInput(Guid.NewGuid(), 1m)).ToList();
        var r = new ProvjeriPromoKodValidator().Validate(new ProvjeriPromoKodQuery("JESEN5", null, stavke));
        Assert.Equal("Korpa ima previše stavki.", Assert.Single(r.Errors).ErrorMessage);

        var bez = new ProvjeriPromoKodValidator().Validate(new ProvjeriPromoKodQuery("JESEN5", null, null!));
        Assert.Equal("Korpa je prazna.", Assert.Single(bez.Errors).ErrorMessage);
    }
}
