using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

public class ZakljucavanjeTests
{
    private static readonly IOptions<KorpiceOpcije> Opcije = Options.Create(new KorpiceOpcije());

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

        // Rok je petak - 48h; testiramo tačno 1 sekund nakon roka.
        var rok = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-Opcije.Value.RokSati)
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

        // Rok je petak - 48h; testiramo tačno 1 sekund prije roka.
        var rok = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-Opcije.Value.RokSati)
            .AddSeconds(-1);

        var broj = await new ZakljucajSadrzajeHandler(t.Db, Opcije)
            .Handle(new ZakljucajSadrzajeCommand(rok), default);

        Assert.Equal(0, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Objavljen, s.Status);
    }

    [Fact]
    public async Task Citava_RokSati_iz_konfiguracije()
    {
        using var t = new TestDb();
        Sadrzaj(t, StatusSadrzaja.Objavljen, 2026, 41);

        // Koristimo RokSati = 24h (umjesto defaultnog 48h).
        // Rok se pomjera sa srijede (Friday - 48h) na četvrtak (Friday - 24h).
        var opcije24 = Options.Create(new KorpiceOpcije { RokSati = 24 });

        // Vrijeme: srijeda podne — nakon defaultnog roka (48h)
        // ali prije configuriranog roka (24h).
        var srijeda = System.Globalization.ISOWeek
            .ToDateTime(2026, 41, DayOfWeek.Friday)
            .AddHours(-48)
            .AddHours(12); // Srijeda podne

        // Sa RokSati=48 (default): srijeda podne > srijeda (Friday - 48h), dakle ZATVARAJ.
        // Sa RokSati=24: srijeda podne < četvrtak (Friday - 24h), dakle NE zatvaraj.
        var broj = await new ZakljucajSadrzajeHandler(t.Db, opcije24)
            .Handle(new ZakljucajSadrzajeCommand(srijeda), default);

        Assert.Equal(0, broj);
        var s = await t.Db.SedmicniSadrzaji.AsNoTracking().SingleAsync();
        Assert.Equal(StatusSadrzaja.Objavljen, s.Status);
    }
}
