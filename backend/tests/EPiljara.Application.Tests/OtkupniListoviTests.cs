using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Nabavka;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Tests;

/// <summary>
/// Otkupni list: kad se sedmica zaključa, za svako gazdinstvo koje ima robu u nabavci nastaje
/// jedan dokument sa snimkom količina i cijena. Scenario ima jedno gazdinstvo (Marko); ovdje se
/// Sir prebacuje drugom (Jelena), da se vidi podjela po gazdinstvima.
/// </summary>
public class OtkupniListoviTests
{
    private sealed record Osnova(Scenario T, Producer Jelena, Guid MarkoId);

    private static async Task<Osnova> NapraviAsync()
    {
        var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var jelena = new Producer { Name = "Jelena", Village = "Šargovac" };
        t.Db.Producers.Add(jelena);
        var sir = await t.Db.Products.SingleAsync(p => p.Id == t.SirId);
        var markoId = sir.ProducerId;
        sir.ProducerId = jelena.Id;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
        return new Osnova(t, jelena, markoId);
    }

    private static async Task ZakljucajSveAsync(Scenario t)
    {
        await t.Db.Orders.ExecuteUpdateAsync(s => s.SetProperty(o => o.ZakljucanaU, DateTime.UtcNow));
        t.Db.ChangeTracker.Clear();
    }

    private static Task<int> NapraviListoveAsync(Scenario t) =>
        new NapraviOtkupneListoveHandler(t.Db, Options.Create(t.Opcije))
            .Handle(new NapraviOtkupneListoveCommand(t.Godina, t.Sedmica), default);

    private static Task<IReadOnlyList<EPiljara.Application.DTOs.OtkupniListDto>> ListoviAsync(Scenario t)
    {
        t.Db.ChangeTracker.Clear();
        return new GetOtkupniListoviHandler(t.Db)
            .Handle(new GetOtkupniListoviQuery(t.Godina, t.Sedmica), default);
    }

    [Fact]
    public async Task Jedan_list_po_gazdinstvu_sa_kolicinama_iz_nabavke()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 3m);
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 2m);
        await o.T.PosaljiArtikalAsync(o.T.SirId, 1m);
        await ZakljucajSveAsync(o.T);

        Assert.Equal(2, await NapraviListoveAsync(o.T));

        var listovi = await ListoviAsync(o.T);
        var marko = listovi.Single(l => l.Prodavac.Naziv == "Marko");
        Assert.Equal(5m, marko.Stavke.Single(s => s.Naziv == "Krompir").Naruceno);
        Assert.DoesNotContain(marko.Stavke, s => s.Naziv == "Sir");
        var jelena = listovi.Single(l => l.Prodavac.Naziv == "Jelena");
        Assert.Equal(1m, jelena.Stavke.Single().Naruceno);
    }

    [Fact]
    public async Task Gazdinstvo_bez_redova_nema_list()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await ZakljucajSveAsync(o.T);

        Assert.Equal(1, await NapraviListoveAsync(o.T));

        Assert.DoesNotContain(await ListoviAsync(o.T), l => l.Prodavac.Naziv == "Jelena");
    }

    [Fact]
    public async Task Otkazana_narudzba_ne_ulazi_u_list()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        var otkazana = await o.T.PosaljiArtikalAsync(o.T.SirId, 1m);
        await o.T.Db.Orders.Where(x => x.Id == otkazana.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, OrderStatus.Cancelled));
        await ZakljucajSveAsync(o.T);

        await NapraviListoveAsync(o.T);

        Assert.DoesNotContain(await ListoviAsync(o.T), l => l.Prodavac.Naziv == "Jelena");
    }

    [Fact]
    public async Task Drugi_poziv_ne_pravi_duplikate()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await o.T.PosaljiArtikalAsync(o.T.SirId, 1m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);

        Assert.Equal(0, await NapraviListoveAsync(o.T));
        Assert.Equal(2, (await ListoviAsync(o.T)).Count);
    }

    [Fact]
    public async Task Brojevi_su_redom_po_godini()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await o.T.PosaljiArtikalAsync(o.T.SirId, 1m);
        await ZakljucajSveAsync(o.T);

        await NapraviListoveAsync(o.T);

        var brojevi = (await ListoviAsync(o.T)).Select(l => l.Broj).OrderBy(b => b).ToList();
        Assert.Equal([$"OL-{o.T.Godina}-0001", $"OL-{o.T.Godina}-0002"], brojevi);
    }

    /// <summary>
    /// Sudar broja (dva upisa istovremeno — posao i dugme admina) mora biti 409 sa kodom, ne 500.
    /// Ovdje se sudar namješta: list druge sedmice već nosi broj koji bi sljedeći dobio.
    /// </summary>
    [Fact]
    public async Task Sudar_broja_vraca_409_umjesto_500()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await ZakljucajSveAsync(o.T);
        o.T.Db.OtkupniListovi.Add(new OtkupniList
        {
            Broj = $"OL-{o.T.Godina}-0001", ProducerId = o.Jelena.Id, Godina = o.T.Godina - 1, Sedmica = 1,
        });
        await o.T.Db.SaveChangesAsync();
        await o.T.Db.OtkupniListovi.ExecuteUpdateAsync(s => s.SetProperty(l => l.Godina, o.T.Godina + 1));
        o.T.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => NapraviListoveAsync(o.T));

        Assert.Equal("otkupni_listovi_u_izradi", ex.Code);
    }

    [Fact]
    public async Task Nezakljucana_sedmica_vraca_sedmica_nije_zakljucana()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => NapraviListoveAsync(o.T));

        Assert.Equal("sedmica_nije_zakljucana", ex.Code);
    }

    [Fact]
    public async Task Admin_otkaz_zakljucane_narudzbe_oznacava_list_zastarjelim()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        var n = await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);

        await new UpdateOrderStatusHandler(o.T.Db, Options.Create(TestOpcije.Poeni()), new FakeEmail())
            .Handle(new UpdateOrderStatusCommand(n.Id, "Cancelled"), default);

        Assert.True((await ListoviAsync(o.T)).Single().Zastario);
    }

    [Fact]
    public async Task Osvjezi_puni_nove_kolicine_i_skida_Zastario()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        var druga = await o.T.PosaljiArtikalAsync(o.T.KrompirId, 2m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);
        await new UpdateOrderStatusHandler(o.T.Db, Options.Create(TestOpcije.Poeni()), new FakeEmail())
            .Handle(new UpdateOrderStatusCommand(druga.Id, "Cancelled"), default);
        var id = (await ListoviAsync(o.T)).Single().Id;

        var dto = await new OsvjeziOtkupniListHandler(o.T.Db, Options.Create(o.T.Opcije))
            .Handle(new OsvjeziOtkupniListCommand(id), default);

        Assert.False(dto.Zastario);
        Assert.Equal(1m, dto.Stavke.Single(s => s.Naziv == "Krompir").Naruceno);
    }

    [Fact]
    public async Task Osvjezi_poslije_unosa_primljenog_vraca_otkupni_list_primljen()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);
        var list = (await ListoviAsync(o.T)).Single();
        await new UnesiPrimljenoHandler(o.T.Db).Handle(new UnesiPrimljenoCommand(list.Id,
            new UnesiPrimljenoInput([new PrimljenoStavkaInput(list.Stavke[0].Id, 1m)])), default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new OsvjeziOtkupniListHandler(o.T.Db, Options.Create(o.T.Opcije))
                .Handle(new OsvjeziOtkupniListCommand(list.Id), default));

        Assert.Equal("otkupni_list_primljen", ex.Code);
    }

    [Fact]
    public async Task Unos_primljenog_racuna_IznosPrimljeno()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.Db.Products.Where(p => p.Id == o.T.KrompirId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NabavnaCijena, 1.5m));
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 4m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);
        var list = (await ListoviAsync(o.T)).Single();

        var dto = await new UnesiPrimljenoHandler(o.T.Db).Handle(new UnesiPrimljenoCommand(list.Id,
            new UnesiPrimljenoInput([new PrimljenoStavkaInput(list.Stavke[0].Id, 3m)])), default);

        Assert.Equal(6m, dto.Iznos);
        Assert.Equal(4.5m, dto.IznosPrimljeno);
        Assert.Equal(3m, dto.Stavke.Single().Primljeno);
    }

    [Fact]
    public async Task Nepoznata_stavka_vraca_stavka_nepoznata()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await ZakljucajSveAsync(o.T);
        await NapraviListoveAsync(o.T);
        var list = (await ListoviAsync(o.T)).Single();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new UnesiPrimljenoHandler(o.T.Db).Handle(new UnesiPrimljenoCommand(list.Id,
                new UnesiPrimljenoInput([new PrimljenoStavkaInput(Guid.NewGuid(), 1m)])), default));

        Assert.Equal("stavka_nepoznata", ex.Code);
    }

    [Fact]
    public async Task Artikal_bez_nabavne_cijene_ima_praznu_cijenu_i_ne_ulazi_u_Iznos()
    {
        var o = await NapraviAsync();
        using var _ = o.T;
        await o.T.Db.Products.Where(p => p.Id == o.T.KrompirId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NabavnaCijena, (decimal?)null));
        await o.T.Db.Products.Where(p => p.Id == o.T.PaprikaId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.NabavnaCijena, 2m));
        await o.T.PosaljiArtikalAsync(o.T.KrompirId, 1m);
        await o.T.PosaljiArtikalAsync(o.T.PaprikaId, 2m);
        await ZakljucajSveAsync(o.T);

        await NapraviListoveAsync(o.T);

        var list = (await ListoviAsync(o.T)).Single();
        Assert.Null(list.Stavke.Single(s => s.Naziv == "Krompir").Cijena);
        Assert.Equal(4m, list.Iznos);
    }
}
