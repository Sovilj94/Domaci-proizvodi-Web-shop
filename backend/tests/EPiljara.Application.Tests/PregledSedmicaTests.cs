using System.Globalization;
using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Početni ekran admina: šta je naručeno za sedmicu koja se pakuje i za otvorenu sedmicu.
/// Srijeda 41/2026 u 18h — rok 41 (uto 16h za čet.) je prošao: pakuje se 41, otvorena je 42.
/// </summary>
public class PregledSedmicaTests
{
    private static readonly DateTime Sada = ISOWeek.ToDateTime(2026, 41, DayOfWeek.Wednesday).AddHours(18);

    private static Task<PregledDto> PregledAsync(PoslovanjePodaciTest p) =>
        new GetPregledHandler(p.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPregledQuery(Sada), default);

    private static async Task<Pretplata> PretplataAsync(PoslovanjePodaciTest p)
    {
        var u = await p.KupacAsync("Pretplatnik");
        var x = new Pretplata
        {
            UserId = u.Id, ProductId = p.Korpica.Id, DeliveryZoneId = p.Centar.Id, DeliverySlotId = p.Slot.Id,
            DeliveryAddress = "Testna 1", Plan = PlanPretplate.Mjesecna, Status = StatusPretplate.Aktivna,
        };
        p.T.Db.Pretplate.Add(x);
        await p.T.Db.SaveChangesAsync();
        return x;
    }

    [Fact]
    public async Task Sedmica_pakovanja_broji_narudzbe_korpice_pretplate_i_spajz_bez_otkazanih()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var pretplata = await PretplataAsync(p);
        await p.NarudzbaAsync(41, OrderStatus.Confirmed, korpica: 2, pretplataId: pretplata.Id);
        await p.NarudzbaAsync(41, OrderStatus.Confirmed, korpica: 1, jaja: 6);
        await p.NarudzbaAsync(41, OrderStatus.Packing, jaja: 10);
        await p.NarudzbaAsync(41, OrderStatus.Cancelled, korpica: 5, jaja: 30);
        await p.NarudzbaAsync(42, OrderStatus.Pending, krompirKg: 3);

        var r = await PregledAsync(p);

        Assert.Equal((2026, 41), (r.Pakovanje.Godina, r.Pakovanje.Sedmica));
        Assert.Equal(3, r.Pakovanje.Narudzbi);
        Assert.Equal(1, r.Pakovanje.IzPretplata);
        Assert.Equal(3, r.Pakovanje.Korpica);
        var mala = Assert.Single(r.Pakovanje.Korpice);
        Assert.Equal(("povrce", "mala", 3, 2), (mala.Tip, mala.Velicina, mala.Komada, mala.IzPretplata));
        var jaja = Assert.Single(r.Pakovanje.Spajz);
        Assert.Equal(("Jaja", "kom", 16m), (jaja.Artikal, jaja.Jedinica, jaja.Kolicina));

        Assert.Equal((2026, 42), (r.Otvorena.Godina, r.Otvorena.Sedmica));
        Assert.Equal(1, r.Otvorena.Narudzbi);
        Assert.Equal(0, r.Otvorena.Korpica);
        var krompir = Assert.Single(r.Otvorena.Spajz);
        Assert.Equal(("Krompir", "kg", 3m), (krompir.Artikal, krompir.Jedinica, krompir.Kolicina));
    }

    /// <summary>
    /// Špajz su samo artikli koji se prodaju pojedinačno (USpajzu): med, zimnica, mliječno.
    /// Voće i povrće idu samo u korpice — i kad su (zaobilaznim putem) naručeni posebno, nisu Špajz.
    /// </summary>
    [Fact]
    public async Task Spajz_su_samo_artikli_u_spajzu_sa_kategorijom()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var krompir = await p.T.Db.Products.FindAsync(p.Krompir.Id);
        krompir!.USpajzu = false;
        await p.T.Db.SaveChangesAsync();
        await p.NarudzbaAsync(42, OrderStatus.Pending, krompirKg: 3, jaja: 10);

        var r = await PregledAsync(p);

        var jaja = Assert.Single(r.Otvorena.Spajz);
        Assert.Equal(("Jaja", "Jaja", 10m), (jaja.Kategorija, jaja.Artikal, jaja.Kolicina));
        Assert.Equal(1, r.Otvorena.Narudzbi);
    }

    /// <summary>Dostupne korpice stoje u tabeli i sa nulom — admin vidi da neka nije naručena.</summary>
    [Fact]
    public async Task Korpice_su_sve_dostupne_po_vrsti_pa_velicini_i_sa_nulom()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var povrce = await p.T.Db.Categories.FindAsync(p.Korpica.CategoryId);
        p.T.Db.Products.AddRange(
            new Product
            {
                Name = "Velika korpica voća", Slug = "velika-korpica-voca", Price = 30m, Unit = Unit.Kom,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Voce, Velicina = VelicinaKorpice.Velika,
                CategoryId = povrce!.Id, ProducerId = p.Korpica.ProducerId,
            },
            new Product
            {
                Name = "Velika korpica povrća", Slug = "velika-korpica-povrca", Price = 25m, Unit = Unit.Kom,
                Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Velika,
                CategoryId = povrce.Id, ProducerId = p.Korpica.ProducerId,
            });
        await p.T.Db.SaveChangesAsync();
        await p.NarudzbaAsync(42, OrderStatus.Pending, korpica: 1);

        var r = await PregledAsync(p);

        Assert.Equal(
            ["povrce/mala", "povrce/velika", "voce/velika"],
            r.Otvorena.Korpice.Select(k => $"{k.Tip}/{k.Velicina}"));
        Assert.Equal([1, 0, 0], r.Otvorena.Korpice.Select(k => k.Komada));
        Assert.Equal(3, r.Pakovanje.Korpice.Count);
        Assert.All(r.Pakovanje.Korpice, k => Assert.Equal(0, k.Komada));
    }

    [Fact]
    public async Task Sedmice_nose_isporuku_i_rok()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();

        var r = await PregledAsync(p);

        Assert.Equal(DayOfWeek.Thursday, r.Otvorena.Isporuka.DayOfWeek);
        Assert.True(r.Otvorena.Rok < r.Otvorena.Isporuka);
        Assert.True(r.Otvorena.Rok > Sada);
        Assert.True(r.Pakovanje.Rok < Sada);
    }
}
