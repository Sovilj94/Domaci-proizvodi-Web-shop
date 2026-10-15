using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Ocjene;
using EPiljara.Application.Features.Utisci;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Admin ocjene (P6a, Task 2): sažetak, lista, utisak iz ocjene.</summary>
public class OcjeneAdminTests
{
    private static int _brojac;

    private static async Task<OcjenaIsporuke> DodajAsync(TestDb t, int ocjena, string komentar = "",
        bool dozvola = false, string kupac = "Mujo Mujić", string zona = "Centar", DateTime? kreirana = null)
    {
        var z = await t.Db.DeliveryZones.FirstOrDefaultAsync(x => x.Name == zona);
        if (z is null)
        {
            z = new DeliveryZone { Name = zona, MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
            t.Db.DeliveryZones.Add(z);
        }
        var slot = await t.Db.DeliverySlots.FirstOrDefaultAsync();
        if (slot is null)
        {
            slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
            t.Db.DeliverySlots.Add(slot);
        }
        await t.Db.SaveChangesAsync();
        var n = ++_brojac;
        var order = new Order
        {
            Code = $"EP-{n:0000}", PublicToken = Guid.NewGuid().ToString("N"), CustomerName = kupac,
            Status = OrderStatus.Delivered, DeliveryAddress = "Testna 1",
            DeliverySlotId = slot.Id, DeliveryZoneId = z.Id,
        };
        var o = new OcjenaIsporuke
        {
            Order = order, Ocjena = ocjena, Komentar = komentar, DozvolaZaObjavu = dozvola,
            Kreirana = kreirana ?? DateTime.UtcNow,
        };
        t.Db.OcjeneIsporuke.Add(o);
        await t.Db.SaveChangesAsync();
        return o;
    }

    private static Task<OcjeneAdminDto> Upit(TestDb t, int? ocjena = null) =>
        new GetAdminOcjeneHandler(t.Db).Handle(new GetAdminOcjeneQuery(ocjena), default);

    private static Task<UtisakDto> Napravi(TestDb t, Guid id) =>
        new NapraviUtisakIzOcjeneHandler(t.Db).Handle(new NapraviUtisakIzOcjeneCommand(id), default);

    [Fact]
    public async Task Sazetak_racuna_prosjek_i_raspodjelu()
    {
        using var t = new TestDb();
        await DodajAsync(t, 5); await DodajAsync(t, 4); await DodajAsync(t, 4);

        var rez = await Upit(t);

        Assert.Equal(4.3m, rez.Sazetak.Prosjek);
        Assert.Equal(3, rez.Sazetak.Broj);
        Assert.Equal([0, 0, 0, 2, 1], rez.Sazetak.PoBroju);
    }

    [Fact]
    public async Task Prazna_tabela_daje_nule()
    {
        using var t = new TestDb();

        var rez = await Upit(t);

        Assert.Equal(0m, rez.Sazetak.Prosjek);
        Assert.Equal(0, rez.Sazetak.Broj);
        Assert.Equal([0, 0, 0, 0, 0], rez.Sazetak.PoBroju);
        Assert.Empty(rez.Stavke);
    }

    [Fact]
    public async Task Filter_suzava_samo_stavke_a_sazetak_ostaje_nad_svim()
    {
        using var t = new TestDb();
        await DodajAsync(t, 5); await DodajAsync(t, 4); await DodajAsync(t, 4);

        var rez = await Upit(t, 4);

        Assert.Equal(2, rez.Stavke.Count);
        Assert.All(rez.Stavke, s => Assert.Equal(4, s.Ocjena));
        Assert.Equal(3, rez.Sazetak.Broj);
        Assert.Equal(4.3m, rez.Sazetak.Prosjek);
    }

    [Fact]
    public async Task Stavke_su_najnovije_prve_sa_kodom_kupcem_i_zonom()
    {
        using var t = new TestDb();
        var stara = await DodajAsync(t, 3, "Staro", kreirana: DateTime.UtcNow.AddDays(-2));
        var nova = await DodajAsync(t, 5, "Novo", true, "Ana Anić", "Borik", DateTime.UtcNow);

        var rez = await Upit(t);

        Assert.Equal([nova.Id, stara.Id], rez.Stavke.Select(s => s.Id));
        var prva = rez.Stavke[0];
        Assert.Equal(nova.OrderId, prva.OrderId);
        Assert.Equal("Ana Anić", prva.Kupac);
        Assert.Equal("Borik", prva.Zona);
        Assert.StartsWith("EP-", prva.KodNarudzbe);
        Assert.True(prva.DozvolaZaObjavu);
        Assert.Equal("Novo", prva.Komentar);
        Assert.Null(prva.UtisakId);
    }

    [Fact]
    public async Task Napravi_utisak_iz_ocjene()
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, "Sve je bilo odlično!", true, "marko marković", "Borik");

        var dto = await Napravi(t, o.Id);

        Assert.Equal("Marko M.", dto.Ime);
        Assert.Equal("Borik", dto.Mjesto);
        Assert.Equal("Sve je bilo odlično!", dto.Tekst);
        Assert.False(dto.Objavljen);
        Assert.Equal(0, dto.Redoslijed);
        Assert.True(dto.IzOcjene);
        t.Db.ChangeTracker.Clear();
        var u = await t.Db.Utisci.SingleAsync();
        Assert.Equal(o.Id, u.OcjenaId);
        Assert.False(u.Objavljen);
        var lista = await Upit(t);
        Assert.Equal(u.Id, lista.Stavke.Single().UtisakId);
    }

    [Theory]
    [InlineData("Marko Marković", "Marko M.")]
    [InlineData("marko", "Marko")]
    [InlineData("  ana  maria  kovač ", "Ana K.")]
    [InlineData("MARKO   MARKOVIĆ", "Marko M.")]
    public async Task Ime_za_utisak(string kupac, string ocekivano)
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, "Komentar dovoljno dug.", true, kupac);

        var dto = await Napravi(t, o.Id);

        Assert.Equal(ocekivano, dto.Ime);
    }

    [Fact]
    public async Task Drugi_put_je_konflikt()
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, "Komentar dovoljno dug.", true);
        await Napravi(t, o.Id);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Napravi(t, o.Id));

        Assert.Equal("utisak_vec_postoji", ex.Code);
    }

    [Theory]
    [InlineData("Komentar dovoljno dug.", false)] // bez dozvole
    [InlineData("", true)]                        // prazan komentar
    [InlineData("Kratko", true)]                  // kraće od 10
    public async Task Nije_za_objavu(string komentar, bool dozvola)
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, komentar, dozvola);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Napravi(t, o.Id));

        Assert.Equal("ocjena_nije_za_objavu", ex.Code);
        Assert.Empty(await t.Db.Utisci.ToListAsync());
    }

    [Fact]
    public async Task Kratak_komentar_ima_poruku()
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, "Kratko", true);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Napravi(t, o.Id));

        Assert.Equal("Komentar je prekratak za utisak.", ex.Message);
    }

    [Fact]
    public async Task Predug_komentar_se_skracuje_na_497_i_tri_tacke()
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, new string('a', 520), true);

        var dto = await Napravi(t, o.Id);

        Assert.Equal(498, dto.Tekst.Length);
        Assert.Equal(new string('a', 497) + "…", dto.Tekst);
    }

    [Fact]
    public async Task Nepostojeca_ocjena_je_404()
    {
        using var t = new TestDb();

        await Assert.ThrowsAsync<NotFoundException>(() => Napravi(t, Guid.NewGuid()));
    }

    [Fact]
    public async Task Brisanje_utiska_oslobadja_ocjenu()
    {
        using var t = new TestDb();
        var o = await DodajAsync(t, 5, "Komentar dovoljno dug.", true);
        var dto = await Napravi(t, o.Id);

        await new DeleteUtisakHandler(t.Db).Handle(new DeleteUtisakCommand(dto.Id), default);
        t.Db.ChangeTracker.Clear();

        var lista = await Upit(t);
        Assert.Null(lista.Stavke.Single().UtisakId);
        var novi = await Napravi(t, o.Id);
        Assert.NotEqual(dto.Id, novi.Id);
    }
}
