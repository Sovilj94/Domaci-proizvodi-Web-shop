using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.Features.Utisci;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;

namespace EPiljara.Application.Tests;

/// <summary>
/// Utisci na početnoj. Javni upit smije vratiti samo objavljene — skriven utisak je
/// admin ostavio za kasnije ili povukao, i ne smije procuriti na početnu.
/// </summary>
public class UtisciTests
{
    private static UtisakInput Ulaz(string ime = "Marija P.", string? mjesto = "Borik",
        string tekst = "Korpica stiže uvijek na vrijeme, a sir je kao kod bake.",
        bool objavljen = true, int redoslijed = 0) =>
        new(ime, mjesto, tekst, objavljen, redoslijed);

    [Fact]
    public async Task Javni_upit_vraca_samo_objavljene_po_redoslijedu()
    {
        using var t = new TestDb();
        t.Db.Utisci.AddRange(
            new Utisak { Ime = "Drugi", Tekst = "Drugi po redu, objavljen.", Objavljen = true, Redoslijed = 2 },
            new Utisak { Ime = "Skriveni", Tekst = "Ovaj ne smije izaći van.", Objavljen = false, Redoslijed = 0 },
            new Utisak { Ime = "Prvi", Tekst = "Prvi po redu, objavljen.", Objavljen = true, Redoslijed = 1 });
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var rez = await new GetUtisciHandler(t.Db).Handle(new GetUtisciQuery(), default);

        Assert.Equal(["Prvi", "Drugi"], rez.Select(u => u.Ime));
    }

    [Fact]
    public async Task Admin_upit_vraca_i_skrivene()
    {
        using var t = new TestDb();
        t.Db.Utisci.AddRange(
            new Utisak { Ime = "Objavljen", Tekst = "Vidljiv na početnoj.", Objavljen = true },
            new Utisak { Ime = "Skriven", Tekst = "Nije vidljiv na početnoj.", Objavljen = false });
        t.Db.SaveChanges();

        var rez = await new GetAdminUtisciHandler(t.Db).Handle(new GetAdminUtisciQuery(), default);

        Assert.Equal(2, rez.Count);
    }

    [Fact]
    public async Task Create_trimuje_i_prazno_mjesto_cuva_kao_prazan_string()
    {
        using var t = new TestDb();

        var dto = await new CreateUtisakHandler(t.Db)
            .Handle(new CreateUtisakCommand(Ulaz(ime: "  Marija  ", mjesto: null)), default);

        Assert.Equal("Marija", dto.Ime);
        Assert.Equal("", dto.Mjesto);
        Assert.Single(t.Db.Utisci);
    }

    [Fact]
    public async Task Update_i_delete_nepostojeceg_daju_404()
    {
        using var t = new TestDb();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateUtisakHandler(t.Db).Handle(new UpdateUtisakCommand(id, Ulaz()), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteUtisakHandler(t.Db).Handle(new DeleteUtisakCommand(id), default));
    }

    [Fact]
    public async Task Update_mijenja_sva_polja()
    {
        using var t = new TestDb();
        var u = new Utisak { Ime = "Staro", Tekst = "Stari tekst utiska.", Objavljen = false };
        t.Db.Utisci.Add(u);
        t.Db.SaveChanges();

        await new UpdateUtisakHandler(t.Db).Handle(
            new UpdateUtisakCommand(u.Id, Ulaz(ime: "Novo", objavljen: true, redoslijed: 5)), default);
        t.Db.ChangeTracker.Clear();

        var iz = t.Db.Utisci.Single();
        Assert.Equal("Novo", iz.Ime);
        Assert.True(iz.Objavljen);
        Assert.Equal(5, iz.Redoslijed);
    }

    [Theory]
    [InlineData("M", "Dovoljno dug tekst utiska.", 0)]          // ime prekratko
    [InlineData("   ", "Dovoljno dug tekst utiska.", 0)]        // ime samo razmaci
    [InlineData("Marija", "Kratko", 0)]                         // tekst ispod 10
    [InlineData("Marija", "Dovoljno dug tekst utiska.", 1000)]  // redoslijed van 0–999
    public void Validator_odbija(string ime, string tekst, int redoslijed)
    {
        var r = new UtisakInputValidator().Validate(Ulaz(ime: ime, tekst: tekst, redoslijed: redoslijed));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validator_odbija_tekst_duzi_od_500()
    {
        var r = new UtisakInputValidator().Validate(Ulaz(tekst: new string('a', 501)));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validator_prihvata_ispravan_ulaz_bez_mjesta()
    {
        var r = new UtisakInputValidator().Validate(Ulaz(mjesto: null));
        Assert.True(r.IsValid);
    }

    [Fact]
    public void Create_validator_daje_kljuceve_input_ime_mjesto_tekst_redoslijed()
    {
        // Ključeve čita admin forma (errors["input.ime"] …); ValidationException ih pravi
        // od PropertyName tako što prvo slovo svakog segmenta spusti u malo.
        var r = new CreateUtisakValidator().Validate(new CreateUtisakCommand(
            Ulaz(ime: "M", mjesto: new string('x', 61), tekst: "Kratko", redoslijed: 1000)));

        var kljucevi = new ValidationException(r.Errors).Errors.Keys.OrderBy(k => k).ToArray();

        Assert.Equal(["input.ime", "input.mjesto", "input.redoslijed", "input.tekst"], kljucevi);
    }
}
