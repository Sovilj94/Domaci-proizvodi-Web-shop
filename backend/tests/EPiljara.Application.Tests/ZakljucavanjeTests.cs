using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class ZakljucavanjeTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije = Options.Create(new KorpiceOpcije());

    /// <summary>Dodaje DeliverySlot u bazu — pozivaocu bira dan i rok, baš kao u produkciji.</summary>
    private static void SeedSlot(TestDb t, int dayOfWeek, int cutoffHours)
    {
        t.Db.DeliverySlots.Add(new DeliverySlot
        {
            DayOfWeek = dayOfWeek, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = cutoffHours,
        });
        t.Db.SaveChanges();
    }

    private static SedmicniSadrzaj Sadrzaj(TestDb t, StatusSadrzaja status, int godina, int sedmica)
    {
        var kat = new Category { Name = "Povrće", Slug = $"povrce-{Guid.NewGuid():N}" };
        var proiz = new Producer { Name = "Mujo Mujić", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);
        var k = new Product
        {
            Name = "Korpica", Slug = $"korpica-{Guid.NewGuid():N}", Price = 25m, Unit = Unit.Kom,
            CategoryId = kat.Id, ProducerId = proiz.Id,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
        };
        t.Db.Products.Add(k);
        var s = new SedmicniSadrzaj { KorpicaId = k.Id, Godina = godina, Sedmica = sedmica, Status = status };
        t.Db.SedmicniSadrzaji.Add(s);
        t.Db.SaveChanges();
        return s;
    }

    [Fact]
    public async Task Zakljucava_objavljene_sadrzaje_kojima_je_rok_istekao()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);

        // Petak sedmice 41/2026 + 1 dan — rok (48h ranije) je davno prošao.
        var poslijeRoka = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday).AddDays(1);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(poslijeRoka), default);

        Assert.Equal(1, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Zakljucan, s.Status);
        Assert.NotNull(s.ZakljucanU);
    }

    [Fact]
    public async Task Ne_dira_sadrzaj_cijim_rokom_jos_ima_vremena()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);

        var prijeRoka = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday).AddHours(-72);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(prijeRoka), default);

        Assert.Equal(0, broj);
        Assert.Equal(StatusSadrzaja.Objavljen, (await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Ne_zakljucava_nacrte()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Nacrt, 2026, 41);

        var poslijeRoka = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday).AddDays(1);

        Assert.Equal(0, await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(poslijeRoka), default));
    }

    [Fact]
    public async Task Zakljucava_sadrzaj_nakon_tocnog_roka()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);
        // Rok dolazi iz slota (petak, 48h), ne iz konfiguracije — granica se ovdje
        // testira na tačnu sekundu, pa mora ići kroz pravi (slot) put.
        SeedSlot(t, dayOfWeek: 5, cutoffHours: 48);

        // Rok je petak - 48h; testiramo tačno 1 sekund nakon roka.
        var rok = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-48)
            .AddSeconds(1);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(rok), default);

        Assert.Equal(1, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Zakljucan, s.Status);
    }

    [Fact]
    public async Task Ne_zakljucava_sadrzaj_prije_tocnog_roka()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);
        // Rok dolazi iz slota (petak, 48h), ne iz konfiguracije — granica se ovdje
        // testira na tačnu sekundu, pa mora ići kroz pravi (slot) put.
        SeedSlot(t, dayOfWeek: 5, cutoffHours: 48);

        // Rok je petak - 48h; testiramo tačno 1 sekund prije roka.
        var rok = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-48)
            .AddSeconds(-1);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(rok), default);

        Assert.Equal(0, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Objavljen, s.Status);
    }

    [Fact]
    public async Task Citava_rok_iz_slota_ne_iz_konfiguracije()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);

        // Slot kaže CutoffHours = 24h — namjerno drugačije od KorpiceOpcije.RokSati
        // default-a (48h) koji ostaje nepromijenjen u Opcije. Dan iz slota je petak,
        // isto kao default rezerva, da izoliramo samo cutoff-osu u ovom testu.
        SeedSlot(t, dayOfWeek: 5, cutoffHours: 24);

        // Vrijeme: srijeda podne — nakon roka po KorpiceOpcije.RokSati (petak - 48h =
        // srijeda podne... dakle TAČNO na granici), pa biramo malo kasnije da budemo
        // jasno nakon "48h" granice, ali jasno PRIJE "24h" granice (četvrtak - 24h).
        var srijeda = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-48)
            .AddHours(12); // Srijeda podne

        // Sa CutoffHours=48 (ono što bi dao Opcije.RokSati ako bi handler pogrešno
        // čitao konfiguraciju umjesto slota): srijeda podne > petak-48h, dakle ZATVARAJ
        // (broj=1) — POGREŠNO, test bi propao.
        // Sa CutoffHours=24 (iz seed-ovanog slota, ono što handler TREBA koristiti):
        // srijeda podne < petak-24h (četvrtak podne), dakle NE zatvaraj (broj=0) — ISPRAVNO.
        // Ova dva ishoda se razlikuju, pa test razlikuje "čita slot" od "čita konfiguraciju".
        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(srijeda), default);

        Assert.Equal(0, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Objavljen, s.Status);
    }

    [Fact]
    public async Task Dan_isporuke_dolazi_iz_slota_sa_najnizim_DayOfWeek()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);

        // Dva slota — ponedjeljak i petak — oba sa istim CutoffHours=48, da izoliramo
        // samo dan-osu. Ponedjeljak ima niži DayOfWeek, pa je on "prvi dan isporuke
        // u sedmici" i handler treba njega da uzme, ne petak (stari hardkod) ni
        // "bilo koji" slot.
        SeedSlot(t, dayOfWeek: 1, cutoffHours: 48); // ponedjeljak
        SeedSlot(t, dayOfWeek: 5, cutoffHours: 48); // petak

        var rokPonedjeljak = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Monday).AddHours(-48);
        var rokPetak = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday).AddHours(-48);

        // Vrijeme je tačno 1h nakon roka za ponedjeljak, ali danima prije roka za petak.
        // Ispravno (dan = ponedjeljak): rok je prošao, ZATVARAJ (broj=1).
        // Pogrešno (dan = petak, stari hardkod): rok nije prošao, NE zatvaraj (broj=0).
        // Ishodi se razlikuju, pa test razlikuje "uzima najniži DayOfWeek" od "uzima petak".
        Assert.True(rokPonedjeljak < rokPetak);
        var sada = rokPonedjeljak.AddHours(1);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(sada), default);

        Assert.Equal(1, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Zakljucan, s.Status);
    }
}
