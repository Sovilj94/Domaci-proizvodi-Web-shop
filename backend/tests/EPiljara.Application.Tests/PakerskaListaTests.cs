using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class PakerskaListaTests
{
    [Fact]
    public async Task Dvije_narudzbe_iste_korpice_daju_dvostruku_kolicinu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(2, lista.BrojNarudzbi);
        var krompir = lista.Stavke.Single(s => s.Artikal == "Krompir");
        Assert.Equal(4m, krompir.Kolicina);   // 2 po korpici × 2 narudžbe
    }

    [Fact]
    public async Task Kolicina_reda_mnozi_sadrzaj()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 3m);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        var krompir = lista.Stavke.Single(s => s.Artikal == "Krompir");
        Assert.Equal(6m, krompir.Kolicina);   // 2 × 3 kutije
    }

    [Fact]
    public async Task Otkazana_narudzba_se_ne_broji()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var zadrzana = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var otkazana = await t.PosaljiKorpicuAsync(kolicina: 1m);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == otkazana.Id);
        o.Status = OrderStatus.Cancelled;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(1, lista.BrojNarudzbi);
        Assert.Equal(2m, lista.Stavke.Single(s => s.Artikal == "Krompir").Kolicina);
    }

    /// <summary>
    /// Nabavlja se samo za potvrđene narudžbe: narudžba na čekanju nije u spisku (samo se
    /// broji u NaCekanju), a čim je admin potvrdi, njena roba i nabavna vrijednost ulaze.
    /// </summary>
    [Fact]
    public async Task Narudzba_na_cekanju_ulazi_u_nabavku_tek_kad_se_potvrdi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        var naCekanju = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var prva = await t.Db.Orders.FirstAsync(x => x.Id != naCekanju.Id);
        prva.Status = OrderStatus.Confirmed;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var prije = await t.PakerskaListaAsync(potvrdiNaCekanju: false);

        Assert.Equal(1, prije.BrojNarudzbi);
        Assert.Equal(1, prije.NaCekanju);
        Assert.Equal(2m, prije.Stavke.Single(s => s.Artikal == "Krompir").Kolicina);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == naCekanju.Id);
        o.Status = OrderStatus.Confirmed;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var poslije = await t.PakerskaListaAsync(potvrdiNaCekanju: false);

        Assert.Equal(2, poslije.BrojNarudzbi);
        Assert.Equal(0, poslije.NaCekanju);
        Assert.Equal(4m, poslije.Stavke.Single(s => s.Artikal == "Krompir").Kolicina);
    }

    [Fact]
    public async Task Bez_ijedne_narudzbe_lista_je_prazna_a_ne_sablon()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(0, lista.BrojNarudzbi);
        Assert.Empty(lista.Stavke);
    }
}
