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
}
