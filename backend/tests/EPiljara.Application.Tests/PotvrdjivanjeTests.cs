using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class PotvrdjivanjeTests
{
    // Sadržaj scenarija: 2 kg krompira i 1 kg paprike po korpici.

    private static async Task PostaviKrompirAsync(Scenario t, decimal stanje)
    {
        var k = await t.Db.Products.FirstAsync(p => p.Id == t.KrompirId);
        k.Stock = stanje;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Na_cekanju_se_potvrdjuje_redom_dok_ima_stanja()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await PostaviKrompirAsync(t, 5m);

        var sedmice = await new GetPotvrdjivanjeHandler(t.Db).Handle(new GetPotvrdjivanjeQuery(), default);

        var k = sedmice.Single().Korpice.Single();
        Assert.Equal(3, k.NaCekanju);
        Assert.Equal(2, k.MozeSePotvrditi);
        Assert.Equal("Krompir", k.Nedostaje);
    }

    [Fact]
    public async Task Potvrdjene_korpice_vec_drze_svoj_dio_stanja()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var potvrdjena = await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        var o = await t.Db.Orders.FirstAsync(x => x.Id == potvrdjena.Id);
        o.Status = OrderStatus.Confirmed;
        await t.Db.SaveChangesAsync();
        await PostaviKrompirAsync(t, 5m);

        var k = (await new GetPotvrdjivanjeHandler(t.Db).Handle(new GetPotvrdjivanjeQuery(), default))
            .Single().Korpice.Single();

        Assert.Equal(2, k.NaCekanju);
        Assert.Equal(1, k.MozeSePotvrditi);   // 5 − 2 potvrđena = 3 kg → još jedna
    }

    [Fact]
    public async Task Bez_narudzbi_na_cekanju_nema_nista()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var o = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var n = await t.Db.Orders.FirstAsync(x => x.Id == o.Id);
        n.Status = OrderStatus.Confirmed;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Empty(await new GetPotvrdjivanjeHandler(t.Db).Handle(new GetPotvrdjivanjeQuery(), default));
    }

    [Fact]
    public async Task Pakerska_lista_sabira_i_Spajz_i_kaze_koliko_fali()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 2m);   // 4 kg krompira u korpicama
        await t.PosaljiObicanArtikalAsync();          // 1 kg krompira iz Špajza
        await PostaviKrompirAsync(t, 3m);

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(2, lista.BrojNarudzbi);
        Assert.False(lista.Zakljucena);
        var krompir = lista.Stavke.Single(s => s.Artikal == "Krompir");
        Assert.Equal(4m, krompir.UKorpicama);
        Assert.Equal(1m, krompir.USpajzu);
        Assert.Equal(5m, krompir.Kolicina);
        Assert.Equal(3m, krompir.NaStanju);
        Assert.Equal(1m, krompir.Fali);
        Assert.DoesNotContain(lista.Stavke, s => s.Artikal == "Mala korpica povrća");
    }
}

public class NabavkaSedmiceTests
{
    [Fact]
    public void Za_nabavku_je_sedmica_prije_one_koja_se_prodaje()
    {
        // Četvrtak 16:00, rok 48 h: u srijedu 29.10.2026. rok sedmice 44 je prošao (utorak 16:00),
        // pa se prodaje 45, a nabavlja za 44.
        var sada = new DateTime(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal((2026, 45), SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada));
        Assert.Equal((2026, 44), SedmicaHelper.ZaNabavku(DayOfWeek.Thursday, "16:00", 48, sada));
    }

    [Fact]
    public async Task Bez_sedmice_lista_je_za_zadnju_zakljucenu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var lista = await new GetPakerskaListaHandler(t.Db, Microsoft.Extensions.Options.Options.Create(t.Opcije))
            .Handle(new GetPakerskaListaQuery(), default);

        Assert.Equal(SedmicaHelper.Pomjeri(t.Godina, t.Sedmica, -1), (lista.Godina, lista.Sedmica));
        Assert.True(lista.Zakljucena);
    }
}
