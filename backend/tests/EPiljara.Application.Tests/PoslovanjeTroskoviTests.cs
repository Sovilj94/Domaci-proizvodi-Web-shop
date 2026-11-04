using EPiljara.Application.Features.Admin.Poslovanje;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Troškovi i dobit mjeseca (spec 2026-11-04-troskovi-design.md). Oktobar 2026:
/// narudžba sedm. 41 isporučena — 3 kg krompira + 1 korpica + 3 KM dostave: neto 19, nabavno 5.
/// Ambalaža: krompir 0,10/kg, korpica (kutija) 1,50 → 1,80.
/// Zakup 200 mjesečno od septembra, marketing 50 jednokratno 3.10.
/// Gorivo 8 l/100 km: 2,50 KM/l od 1.1., 3,00 KM/l od 10.10. Ruta 8.10. 20 km → 4,00; ruta 22.10. 10 km → 2,40;
/// ruta 15.10. samo sa otkazanom narudžbom ne broji se.
/// </summary>
public class PoslovanjeTroskoviTests
{
    private static async Task<(PoslovanjePodaciTest P, VrstaTroska Zakup)> OktobarAsync()
    {
        var p = await PoslovanjePodaciTest.NapraviAsync();
        var db = p.T.Db;
        (await db.Products.FirstAsync(x => x.Id == p.Krompir.Id)).AmbalazaCijena = 0.10m;
        (await db.Products.FirstAsync(x => x.Id == p.Korpica.Id)).AmbalazaCijena = 1.50m;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var isporucena = await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 1, dostava: 3);
        var otkazana = await p.NarudzbaAsync(42, OrderStatus.Cancelled, krompirKg: 5);
        var isporucena2 = await p.NarudzbaAsync(43, OrderStatus.Delivered, jaja: 1);

        var zakup = new VrstaTroska { Naziv = "Zakup", Redoslijed = 1 };
        var marketing = new VrstaTroska { Naziv = "Marketing", Redoslijed = 2 };
        db.AddRange(zakup, marketing);
        db.PonavljajuciTroskovi.Add(new PonavljajuciTrosak { Vrsta = zakup, Iznos = 200m, Opis = "Magacin", OdGodina = 2026, OdMjesec = 9 });
        db.Troskovi.Add(new Trosak { Vrsta = marketing, Datum = new DateOnly(2026, 10, 3), Iznos = 50m, Opis = "Letci" });
        db.Troskovi.Add(new Trosak { Vrsta = marketing, Datum = new DateOnly(2026, 11, 1), Iznos = 999m, Opis = "Novembar" });
        db.CijeneGoriva.Add(new CijenaGoriva { VaziOd = new DateOnly(2026, 1, 1), CijenaPoLitru = 2.50m, PotrosnjaL100km = 8m });
        db.CijeneGoriva.Add(new CijenaGoriva { VaziOd = new DateOnly(2026, 10, 10), CijenaPoLitru = 3.00m, PotrosnjaL100km = 8m });

        var vozac = new Driver { Name = "Dragan", Phone = "065 000 000" };
        db.Drivers.Add(vozac);
        void Ruta(DateOnly datum, double metara, Guid orderId)
        {
            var r = new DeliveryRoute { Date = datum, DriverId = vozac.Id, Naziv = "Centar", KilometrazaMetara = metara, Status = RouteStatus.Completed };
            db.DeliveryRoutes.Add(r);
            db.RouteStops.Add(new RouteStop { RouteId = r.Id, OrderId = orderId, Sequence = 1 });
        }
        Ruta(new DateOnly(2026, 10, 8), 20_000, isporucena.Id);
        Ruta(new DateOnly(2026, 10, 15), 50_000, otkazana.Id);
        Ruta(new DateOnly(2026, 10, 22), 10_000, isporucena2.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (p, zakup);
    }

    [Fact]
    public async Task Troskovi_mjeseca_saberu_ambalazu_gorivo_po_rutama_i_ostale()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;

        var t = (await p.MjesecAsync(10)).Troskovi;

        Assert.Equal(1.80m, t.Ambalaza);
        Assert.Equal(30, t.Gorivo.Km);
        Assert.Equal(2, t.Gorivo.BrojRuta);
        Assert.Equal(6.40m, t.Gorivo.PoRutama);
        Assert.Equal(0m, t.Gorivo.PoRacunima);
        Assert.Equal("rute", t.Gorivo.Izvor);
        Assert.Equal(6.40m, t.Gorivo.UDobiti);
        Assert.Equal(250m, t.Ostali);
        Assert.Equal(["Zakup", "Marketing"], t.PoVrsti.Select(v => v.Vrsta));
        Assert.Equal(200m, t.PoVrsti[0].Ponavljajuci);
        Assert.Equal(258.20m, t.Ukupno);
    }

    [Fact]
    public async Task Dnevna_tura_i_placeno_gorivo_vozaca_zamjenjuju_procjenu_po_rutama()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;
        var db = p.T.Db;
        var vozac = await db.Drivers.FirstAsync();
        // 8.10.: dnevna tura 30 km (umjesto 20 po ruti), vozač platio 7,00 KM; 22.10. ostaje po ruti (10 km → 2,40).
        db.DnevneVoznje.Add(new DnevnaVoznja { DriverId = vozac.Id, Datum = new DateOnly(2026, 10, 8), ProcjenaMetara = 30_000, PlacenoGorivo = 7m });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var g = (await p.MjesecAsync(10)).Troskovi.Gorivo;

        Assert.Equal(40, g.Km);
        Assert.Equal(2, g.BrojDana);
        Assert.Equal(9.40m, g.PoRutama);
        Assert.Equal(8.40m, g.Procijenjeno);
        Assert.Equal(7m, g.PlacenoVozaci);
        Assert.Equal(1, g.DanaSaUnosom);
        Assert.Equal(9.40m, g.UDobiti);
    }

    [Fact]
    public async Task Dobit_je_neto_manje_nabavno_manje_troskovi()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;

        var d = await p.MjesecAsync(10);

        // Neto 19 + 0,50 (jaja) = 19,50; nabavno 5 + 0,30 = 5,30; troškovi 258,20.
        Assert.Equal(19.50m, d.Prihod.Neto);
        Assert.Equal(-244.00m, d.Troskovi.Dobit);
        Assert.NotNull(d.Troskovi.MarzaDobiti);
        // Septembar: samo zakup.
        Assert.Equal(200m, d.PrethodniTroskovi.Ukupno);
    }

    [Fact]
    public async Task Racuni_za_gorivo_zamjenjuju_procjenu_po_rutama()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;
        var gorivo = new VrstaTroska { Naziv = "Gorivo", JeGorivo = true };
        p.T.Db.Troskovi.Add(new Trosak { Vrsta = gorivo, Datum = new DateOnly(2026, 10, 20), Iznos = 80m, Opis = "Petrol" });
        await p.T.Db.SaveChangesAsync();

        var t = (await p.MjesecAsync(10)).Troskovi;

        Assert.Equal("racuni", t.Gorivo.Izvor);
        Assert.Equal(80m, t.Gorivo.PoRacunima);
        Assert.Equal(6.40m, t.Gorivo.PoRutama);
        Assert.Equal(80m, t.Gorivo.UDobiti);
        Assert.Equal(250m, t.Ostali); // gorivo nije u „ostalim"
        Assert.Equal(1.80m + 80m + 250m, t.Ukupno);
    }

    [Fact]
    public async Task Ruta_bez_cijene_goriva_se_prijavljuje()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;
        p.T.Db.CijeneGoriva.RemoveRange(p.T.Db.CijeneGoriva);
        await p.T.Db.SaveChangesAsync();

        var t = (await p.MjesecAsync(10)).Troskovi;

        Assert.True(t.Gorivo.BezCijene);
        Assert.Null(t.Gorivo.PoRutama);
        Assert.Equal(0m, t.Gorivo.UDobiti);
    }

    [Fact]
    public async Task Ponavljajuci_trosak_vazi_samo_u_svom_opsegu()
    {
        var (p, zakup) = await OktobarAsync();
        using var _p = p;
        var r = await p.T.Db.PonavljajuciTroskovi.FirstAsync();
        (r.DoGodina, r.DoMjesec) = (2026, 9); // zaustavljen poslije septembra
        await p.T.Db.SaveChangesAsync();

        var t = (await p.MjesecAsync(10)).Troskovi;

        Assert.DoesNotContain(t.PoVrsti, v => v.Vrsta == "Zakup");
        Assert.Equal(50m, t.Ostali);
    }

    [Fact]
    public async Task Godina_ima_troskove_i_dobit_po_mjesecu()
    {
        var (p, _) = await OktobarAsync();
        using var _p = p;

        var g = await new GetPoslovanjeGodinaHandler(p.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPoslovanjeGodinaQuery(2026), default);

        var okt = g.Mjeseci.Single(m => m.Mjesec == 10);
        Assert.Equal(258.20m, okt.Troskovi);
        Assert.Equal(-244.00m, okt.Dobit);
        Assert.Equal(200m, g.Mjeseci.Single(m => m.Mjesec == 9).Troskovi);
        Assert.Equal(0m, g.Mjeseci.Single(m => m.Mjesec == 8).Troskovi);
    }
}
