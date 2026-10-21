using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Jednostavnije mijenjanje korpice: „izaberi umjesto mene", kupčev spisak „ne stavljaj mi"
/// (pri zamjeni, pri narudžbi i pri vraćanju), „vrati kako je bilo" i adminova izmjena
/// korpice jednog kupca sa količinama.
/// </summary>
public class JednostavnaKorpicaTests
{
    /// <summary>Scenario (cilj 7.00 = Krompir 2kg×2 + Paprika 1kg×3) + Tikvica 1 KM/kg, cijele godine.</summary>
    private static async Task<(Scenario s, Product tikvica)> SaTikvicom()
    {
        var s = await SaKupcem();
        var krompir = await s.Db.Products.SingleAsync(p => p.Id == s.KrompirId);
        var tikvica = new Product
        {
            Name = "Tikvica", Slug = "tikvica", Price = 1m, Unit = Unit.Kg, Stock = 100m,
            MozeUKorpicu = true, CategoryId = krompir.CategoryId, ProducerId = krompir.ProducerId,
        };
        s.Db.Products.Add(tikvica);
        await s.Db.SaveChangesAsync();
        return (s, tikvica);
    }

    /// <summary>Prijavljen kupac sa redom u Users — spisak „ne stavljaj mi" ima strani ključ na njega.</summary>
    private static async Task<Scenario> SaKupcem()
    {
        var s = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        s.Db.Users.Add(new User { Id = s.KupacId, Name = "Marko Marković", Email = "marko@example.com" });
        await s.Db.SaveChangesAsync();
        return s;
    }

    private static async Task<Guid> Naruci(Scenario s)
    {
        var narudzba = await s.PosaljiKorpicuAsync(1m);
        var id = await s.OrderItemIdAsync(narudzba.Id);
        s.Db.ChangeTracker.Clear();
        return id;
    }

    [Fact]
    public async Task Izaberi_umjesto_mene_uzima_prvu_zamjenu_koja_staje()
    {
        var (s, tikvica) = await SaTikvicom();
        using var _ = s;
        var id = await Naruci(s);

        var r = await s.ZamijeniAsync(id, s.KrompirId, ubaci: null, zapamti: false);

        Assert.Equal(4m, Assert.Single(r.Stavke, x => x.ProductId == tikvica.Id).Kolicina);
        Assert.DoesNotContain(r.Stavke, x => x.ProductId == s.KrompirId);
        Assert.Equal(7m, r.Korpica.TrenutnaVrijednost);
        Assert.Empty(r.Izbjegnuti);
    }

    [Fact]
    public async Task Izaberi_umjesto_mene_bira_svjeze_prije_dopune_i_kad_je_dopuna_preporucena()
    {
        var (s, tikvica) = await SaTikvicom();
        using var _ = s;
        var grah = new Product
        {
            Name = "Grah", Slug = "grah", Price = 1m, Unit = Unit.Kg, Stock = 100m, PrioritetUKorpici = 1,
            MozeUKorpicu = true, CategoryId = tikvica.CategoryId, ProducerId = tikvica.ProducerId,
        };
        s.Db.Products.Add(grah);
        var sadrzaj = await s.Db.SedmicniSadrzaji.SingleAsync(x => x.KorpicaId == s.KorpicaId);
        s.Db.ZamjeneSadrzaja.Add(new ZamjenaSadrzaja { SedmicniSadrzajId = sadrzaj.Id, ProductId = grah.Id, JedinicnaCijena = 1m });
        await s.Db.SaveChangesAsync();
        var id = await Naruci(s);

        var r = await s.ZamijeniAsync(id, s.KrompirId, ubaci: null, zapamti: false);

        Assert.Contains(r.Stavke, x => x.ProductId == tikvica.Id);
        Assert.DoesNotContain(r.Stavke, x => x.ProductId == grah.Id);
    }

    [Fact]
    public async Task Bez_ijedne_zamjene_koja_staje_javlja_nema_zamjene()
    {
        var s = await SaKupcem();
        using var _ = s;
        // Svi ostali artikli scenarija su skupi, van sezone ili bez zalihe — ništa ne staje umjesto paprike.
        await s.Db.Products.Where(p => p.Id != s.KrompirId && p.Id != s.PaprikaId && p.Vrsta == VrstaProizvoda.Obican)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.MozeUKorpicu, false));
        var id = await Naruci(s);

        var ex = await Assert.ThrowsAsync<DomainException>(() => s.ZamijeniAsync(id, s.PaprikaId, null, false));
        Assert.Equal("nema_zamjene", ex.Code);
    }

    [Fact]
    public async Task Zapamti_stavlja_artikal_na_spisak_i_sljedeca_narudzba_ga_odmah_mijenja()
    {
        var (s, tikvica) = await SaTikvicom();
        using var _ = s;
        var prva = await Naruci(s);

        var r = await s.ZamijeniAsync(prva, s.KrompirId, tikvica.Id, zapamti: true);
        Assert.Contains(s.KrompirId, r.Izbjegnuti);

        // Nova narudžba: krompir je na spisku, pa ga snimak korpice odmah zamijeni tikvicom.
        var druga = await Naruci(s);
        var stavke = await s.Db.StavkeKorpiceNarudzbe.Where(x => x.OrderItemId == druga).ToListAsync();
        Assert.DoesNotContain(stavke, x => x.ProductId == s.KrompirId);
        var nova = Assert.Single(stavke, x => x.ProductId == tikvica.Id);
        Assert.Equal(4m, nova.Kolicina);
        Assert.True(nova.DodaoKupac);
        Assert.Equal(7m, stavke.Sum(x => x.Kolicina * x.JedinicnaCijena));
    }

    [Fact]
    public async Task Gost_i_kupac_bez_spiska_dobijaju_sadrzaj_sedmice_netaknut()
    {
        var (s, _) = await SaTikvicom();
        using var __ = s;
        var id = await Naruci(s);

        var stavke = await s.Db.StavkeKorpiceNarudzbe.Where(x => x.OrderItemId == id).ToListAsync();
        Assert.Equal([s.KrompirId, s.PaprikaId], stavke.Select(x => x.ProductId).OrderBy(x => x == s.PaprikaId).ToArray());
        Assert.All(stavke, x => Assert.False(x.DodaoKupac));
    }

    [Fact]
    public async Task Vrati_kako_je_bilo_vraca_sadrzaj_sedmice_uz_spisak()
    {
        var (s, tikvica) = await SaTikvicom();
        using var _ = s;
        var id = await Naruci(s);
        await s.ZamijeniAsync(id, s.KrompirId, tikvica.Id, zapamti: false);
        s.Db.ChangeTracker.Clear();

        var vraceno = await s.VratiAsync(id);
        Assert.Contains(vraceno.Stavke, x => x.ProductId == s.KrompirId && x.Kolicina == 2m && !x.DodaoKupac);
        Assert.DoesNotContain(vraceno.Stavke, x => x.ProductId == tikvica.Id);

        // Sa krompirom na spisku, vraćanje ga opet mijenja — „kako je bilo" znači kao pri narudžbi.
        s.Db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = s.KupacId, ProductId = s.KrompirId });
        await s.Db.SaveChangesAsync();
        s.Db.ChangeTracker.Clear();
        var saSpiskom = await s.VratiAsync(id);
        Assert.DoesNotContain(saSpiskom.Stavke, x => x.ProductId == s.KrompirId);
        Assert.Contains(saSpiskom.Stavke, x => x.ProductId == tikvica.Id);
    }

    [Fact]
    public async Task Admin_mijenja_kolicine_i_dodaje_artikal_bez_granica_budzeta()
    {
        var (s, tikvica) = await SaTikvicom();
        using var _ = s;
        var id = await Naruci(s);
        s.Db.IzbjegnutiArtikli.Add(new IzbjegnutArtikal { UserId = s.KupacId, ProductId = s.PaprikaId });
        await s.Db.SaveChangesAsync();

        var prije = await new GetAdminKorpicaNarudzbeHandler(s.Db).Handle(new GetAdminKorpicaNarudzbeQuery(id), default);
        Assert.Equal(7m, prije.Vrijednost);
        Assert.Equal(7m, prije.Cilj);
        Assert.True(prije.MozeSeMijenjati);
        Assert.Equal("Paprika", Assert.Single(prije.Izbjegnuti).Naziv);

        // Kupac je zvao: bez paprike, duplo krompira, i tikvica.
        var r = await new IzmijeniKorpicuNarudzbeHandler(s.Db).Handle(new IzmijeniKorpicuNarudzbeCommand(id,
            [new StavkaUnos(s.KrompirId, 4m), new StavkaUnos(tikvica.Id, 3m)]), default);

        Assert.Equal(11m, r.Vrijednost);                    // 4×2 + 3×1 — admin smije preko cilja
        Assert.False(Assert.Single(r.Stavke, x => x.ProductId == s.KrompirId).Izmijenjeno);
        Assert.True(Assert.Single(r.Stavke, x => x.ProductId == tikvica.Id).Izmijenjeno);
        Assert.DoesNotContain(r.Stavke, x => x.ProductId == s.PaprikaId);
    }

    [Fact]
    public async Task Admin_ne_mijenja_korpicu_u_dostavi_ni_komad_na_pola()
    {
        var (s, _) = await SaTikvicom();
        using var __ = s;
        var id = await Naruci(s);
        var sir = await s.Db.Products.SingleAsync(p => p.Id == s.SirId);
        Assert.Equal(Unit.Kom, sir.Unit);

        var pola = await Assert.ThrowsAsync<DomainException>(() => new IzmijeniKorpicuNarudzbeHandler(s.Db)
            .Handle(new IzmijeniKorpicuNarudzbeCommand(id, [new StavkaUnos(s.SirId, 1.5m)]), default));
        Assert.Equal("kolicina_na_komad", pola.Code);

        await s.Db.Orders.ExecuteUpdateAsync(x => x.SetProperty(o => o.Status, OrderStatus.OutForDelivery));
        s.Db.ChangeTracker.Clear();
        var dostava = await Assert.ThrowsAsync<ConflictException>(() => new IzmijeniKorpicuNarudzbeHandler(s.Db)
            .Handle(new IzmijeniKorpicuNarudzbeCommand(id, [new StavkaUnos(s.KrompirId, 1m)]), default));
        Assert.Equal("korpica_u_dostavi", dostava.Code);
    }
}
