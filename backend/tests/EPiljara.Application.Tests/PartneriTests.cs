using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.Features.Partneri;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>Prijave partnera: javni upis sa honeypotom (Review Focus 4) i admin obrada.</summary>
public class PartneriTests
{
    private static PrijavaInput Ulaz(string ime = "Marko Marković", string telefon = "065 123 456",
        string? email = null, string proizvodi = "Med i kajmak, oko 50 kg sedmično", string? web = null) =>
        new(ime, null, "Stričići", telefon, email, proizvodi, web);

    private static PartnerPrijava Nova(TestDb t, StatusPrijave status, DateTime kreirana, string ime = "Ime Prezime")
    {
        var p = new PartnerPrijava
        {
            Ime = ime, Mjesto = "Selo", Telefon = "065123456", Proizvodi = "Sir i mlijeko",
            Status = status, Kreirana = kreirana,
        };
        t.Db.PartnerPrijave.Add(p);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return p;
    }

    [Fact]
    public async Task Prijava_se_upisuje_kao_Nova_trimovana_sa_praznim_emailom()
    {
        using var t = new TestDb();
        var r = await new PosaljiPrijavuHandler(t.Db).Handle(new PosaljiPrijavuCommand(
            new PrijavaInput("  Marko Marković ", null, " Stričići ", " 065 123 456 ", null, "  Med i kajmak  ", null)),
            CancellationToken.None);

        Assert.True(r.Primljena);
        t.Db.ChangeTracker.Clear();
        var p = await t.Db.PartnerPrijave.SingleAsync();
        Assert.Equal(StatusPrijave.Nova, p.Status);
        Assert.Equal("Marko Marković", p.Ime);
        Assert.Equal("Stričići", p.Mjesto);
        Assert.Equal("065 123 456", p.Telefon);
        Assert.Equal("Med i kajmak", p.Proizvodi);
        Assert.Equal("", p.Email);
        Assert.Equal("", p.Gazdinstvo);
        Assert.Equal("", p.AdminNapomena);
    }

    [Fact]
    public async Task Honeypot_vraca_isti_rezultat_a_ne_upisuje_nista()
    {
        using var t = new TestDb();
        var r = await new PosaljiPrijavuHandler(t.Db).Handle(
            new PosaljiPrijavuCommand(Ulaz(web: "http://spam")), CancellationToken.None);

        Assert.True(r.Primljena);
        Assert.Empty(await t.Db.PartnerPrijave.ToListAsync());
    }

    [Fact]
    public void Validator_odbija_lose_i_prihvata_ispravne_prijave()
    {
        var v = new PosaljiPrijavuValidator();
        Assert.False(v.Validate(new PosaljiPrijavuCommand(Ulaz(telefon: ""))).IsValid);
        Assert.False(v.Validate(new PosaljiPrijavuCommand(Ulaz(telefon: "123"))).IsValid);
        Assert.False(v.Validate(new PosaljiPrijavuCommand(Ulaz(telefon: "065-abc-123"))).IsValid);
        Assert.False(v.Validate(new PosaljiPrijavuCommand(Ulaz(email: "nije-mejl"))).IsValid);
        Assert.False(v.Validate(new PosaljiPrijavuCommand(Ulaz(proizvodi: "med"))).IsValid);
        Assert.True(v.Validate(new PosaljiPrijavuCommand(Ulaz())).IsValid);
        Assert.True(v.Validate(new PosaljiPrijavuCommand(Ulaz(email: "marko@example.com"))).IsValid);
        // Honeypot ne smije dati 400: bot ne uči da je otkriven.
        Assert.True(v.Validate(new PosaljiPrijavuCommand(Ulaz(web: "http://spam"))).IsValid);
    }

    [Fact]
    public void Validator_daje_kljuceve_input_telefon_i_input_email()
    {
        var r = new PosaljiPrijavuValidator().Validate(
            new PosaljiPrijavuCommand(Ulaz(telefon: "1", email: "nije-mejl")));

        var kljucevi = new ValidationException(r.Errors).Errors.Keys.OrderBy(k => k).ToArray();

        Assert.Equal(["input.email", "input.telefon"], kljucevi);
    }

    [Fact]
    public async Task Admin_lista_filtrira_po_statusu_i_vraca_najnovije_prve()
    {
        using var t = new TestDb();
        var sad = DateTime.UtcNow;
        Nova(t, StatusPrijave.Nova, sad.AddDays(-3), "Stara");
        Nova(t, StatusPrijave.Nova, sad.AddDays(-1), "Novija");
        Nova(t, StatusPrijave.Odbijena, sad.AddDays(-2), "Odbijena");
        var h = new GetAdminPartneriHandler(t.Db);

        var sve = await h.Handle(new GetAdminPartneriQuery(null), CancellationToken.None);
        Assert.Equal(["Novija", "Odbijena", "Stara"], sve.Select(x => x.Ime).ToArray());

        var nove = await h.Handle(new GetAdminPartneriQuery("nova"), CancellationToken.None);
        Assert.Equal(["Novija", "Stara"], nove.Select(x => x.Ime).ToArray());
        Assert.All(nove, x => Assert.Equal("Nova", x.Status));

        var odbijene = await h.Handle(new GetAdminPartneriQuery("Odbijena"), CancellationToken.None);
        Assert.Single(odbijene);

        var nevaljan = await h.Handle(new GetAdminPartneriQuery("nesto"), CancellationToken.None);
        Assert.Equal(3, nevaljan.Count);
    }

    [Fact]
    public async Task Patch_mijenja_status_i_napomenu_nepostojeci_je_404()
    {
        using var t = new TestDb();
        var p = Nova(t, StatusPrijave.Nova, DateTime.UtcNow);
        var h = new ObradiPrijavuHandler(t.Db);

        var dto = await h.Handle(new ObradiPrijavuCommand(p.Id, new ObradaInput("uObradi", " Zvati u ponedjeljak ")),
            CancellationToken.None);

        Assert.Equal("UObradi", dto.Status);
        Assert.Equal("Zvati u ponedjeljak", dto.AdminNapomena);
        t.Db.ChangeTracker.Clear();
        var u = await t.Db.PartnerPrijave.SingleAsync();
        Assert.Equal(StatusPrijave.UObradi, u.Status);
        Assert.Equal("Zvati u ponedjeljak", u.AdminNapomena);

        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(
            new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("Nova", null)), CancellationToken.None));
    }

    [Fact]
    public void Patch_validator_odbija_status_izvan_enuma_i_daje_kljuc_input_status()
    {
        var v = new ObradiPrijavuValidator();
        Assert.False(v.Validate(new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("99", null))).IsValid);
        Assert.False(v.Validate(new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("nesto", null))).IsValid);
        Assert.False(v.Validate(new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("", null))).IsValid);
        Assert.True(v.Validate(new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("prihvacena", null))).IsValid);

        var r = v.Validate(new ObradiPrijavuCommand(Guid.NewGuid(), new ObradaInput("99", new string('x', 1001))));
        var kljucevi = new ValidationException(r.Errors).Errors.Keys.OrderBy(k => k).ToArray();
        Assert.Equal(["input.adminNapomena", "input.status"], kljucevi);
    }

    [Fact]
    public void Telefon_prekratak_vraca_srpsku_poruku()
    {
        var r = new PrijavaInputValidator().Validate(Ulaz(telefon: "12"));

        var poruka = Assert.Single(r.Errors, e => e.PropertyName == "telefon").ErrorMessage;
        Assert.Equal("Telefon mora imati od 6 do 30 znakova.", poruka);
        Assert.DoesNotContain("must be", poruka);
    }

    [Fact]
    public void Validatori_nemaju_engleske_default_poruke()
    {
        var dugo = new string('x', 2000);
        var greske = new PrijavaInputValidator()
            .Validate(new PrijavaInput("a", dugo, "b", "ab", dugo, "c", null)).Errors
            .Concat(new ObradaInputValidator().Validate(new ObradaInput("99", dugo)).Errors)
            .Concat(new PosaljiPrijavuValidator().Validate(new PosaljiPrijavuCommand(null!)).Errors)
            .Concat(new ObradiPrijavuValidator().Validate(new ObradiPrijavuCommand(Guid.Empty, null!)).Errors)
            .ToList();

        Assert.NotEmpty(greske);
        Assert.All(greske, e =>
        {
            Assert.DoesNotContain("must", e.ErrorMessage);
            Assert.DoesNotContain("'", e.ErrorMessage);
        });
    }
}
