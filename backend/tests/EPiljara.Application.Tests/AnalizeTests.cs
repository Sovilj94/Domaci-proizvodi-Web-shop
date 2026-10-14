using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Analize;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>
/// Analize. Javno izlaze samo objavljene (i uskladjene i neuskladjene — spec O12),
/// a proizvođač s analizom se ne briše (Review Focus 3).
/// </summary>
public class AnalizeTests
{
    private static Producer NoviProizvodjac(TestDb t, string ime = "Porodica Kovačević", string selo = "Stričići")
    {
        var p = new Producer { Name = ime, Village = selo };
        t.Db.Producers.Add(p);
        t.Db.SaveChanges();
        return p;
    }

    private static Analiza NovaAnaliza(TestDb t, Producer p, DateOnly datum, bool objavljena = true,
        RezultatAnalize rezultat = RezultatAnalize.Uskladjeno, DateTime? kreiran = null, Product? proizvod = null)
    {
        var a = new Analiza
        {
            ProducerId = p.Id, ProductId = proizvod?.Id, Uzorak = "Med bagrem", Laboratorija = "Zavod za zdravstvenu zaštitu",
            BrojIzvjestaja = "123/26", DatumUzorkovanja = datum, Ispitano = "Voda, HMF, dijastaza",
            Rezultat = rezultat, Napomena = "", Objavljena = objavljena, Kreiran = kreiran ?? DateTime.UtcNow,
        };
        t.Db.Analize.Add(a);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return a;
    }

    private static AnalizaInput Ulaz(Guid producerId, Guid? productId = null, string uzorak = "Med bagrem",
        string laboratorija = "Zavod", string ispitano = "Voda, HMF", DateOnly? datum = null, string rezultat = "Uskladjeno") =>
        new(producerId, productId, uzorak, laboratorija, null, datum ?? new DateOnly(2026, 5, 1),
            ispitano, rezultat, null, null, true);

    [Fact]
    public async Task Javna_lista_vraca_samo_objavljene_po_datumu_pa_kreiranju_opadajuce()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);
        var sad = DateTime.UtcNow;
        var stara = NovaAnaliza(t, p, new DateOnly(2026, 1, 10));
        var novaPrva = NovaAnaliza(t, p, new DateOnly(2026, 4, 1), kreiran: sad.AddMinutes(-5));
        var novaDruga = NovaAnaliza(t, p, new DateOnly(2026, 4, 1), kreiran: sad);
        NovaAnaliza(t, p, new DateOnly(2026, 6, 1), objavljena: false);

        var r = await new GetAnalizeHandler(t.Db).Handle(new GetAnalizeQuery(null), default);

        Assert.Equal([novaDruga.Id, novaPrva.Id, stara.Id], r.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Filter_producer_vraca_samo_analize_tog_proizvodjaca()
    {
        using var t = new TestDb();
        var a = NoviProizvodjac(t, "A", "Selo A");
        var b = NoviProizvodjac(t, "B", "Selo B");
        var aa = NovaAnaliza(t, a, new DateOnly(2026, 3, 1));
        NovaAnaliza(t, b, new DateOnly(2026, 3, 2));

        var r = await new GetAnalizeHandler(t.Db).Handle(new GetAnalizeQuery(a.Id), default);

        Assert.Equal([aa.Id], r.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Dto_nosi_proizvodjaca_selo_i_proizvod_a_neobjavljena_nijeUskladjeno_se_ne_vraca()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t, "Porodica Kovačević", "Stričići");
        var kat = new Category { Name = "Med", Slug = "med" };
        t.Db.Categories.Add(kat);
        t.Db.SaveChanges();
        var proizvod = new Product
        {
            Name = "Bagremov med", Slug = "bagremov-med", Price = 9m, Unit = Unit.Kom, CategoryId = kat.Id,
            ProducerId = p.Id, Stock = 5, IsAvailable = true,
        };
        t.Db.Products.Add(proizvod);
        t.Db.SaveChanges();

        var objavljenaNeuskladjena = NovaAnaliza(t, p, new DateOnly(2026, 2, 1), rezultat: RezultatAnalize.NijeUskladjeno, proizvod: proizvod);
        NovaAnaliza(t, p, new DateOnly(2026, 3, 1), objavljena: false, rezultat: RezultatAnalize.NijeUskladjeno);

        var r = await new GetAnalizeHandler(t.Db).Handle(new GetAnalizeQuery(null), default);

        var dto = Assert.Single(r);
        Assert.Equal(objavljenaNeuskladjena.Id, dto.Id);
        Assert.Equal("NijeUskladjeno", dto.Rezultat);
        Assert.Equal("Porodica Kovačević", dto.Proizvodjac);
        Assert.Equal("Stričići", dto.Selo);
        Assert.Equal("Bagremov med", dto.Proizvod);
        Assert.Equal(proizvod.Id, dto.ProductId);
        Assert.Equal(new DateOnly(2026, 2, 1), dto.DatumUzorkovanja);
    }

    [Fact]
    public async Task Create_sa_nepostojecim_proizvodjacem_ili_proizvodom_baca_DomainException()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);
        var h = new CreateAnalizaHandler(t.Db);

        var e1 = await Assert.ThrowsAsync<DomainException>(() =>
            h.Handle(new CreateAnalizaCommand(Ulaz(Guid.NewGuid())), default));
        Assert.Equal("proizvodjac_ne_postoji", e1.Code);

        var e2 = await Assert.ThrowsAsync<DomainException>(() =>
            h.Handle(new CreateAnalizaCommand(Ulaz(p.Id, Guid.NewGuid())), default));
        Assert.Equal("proizvod_ne_postoji", e2.Code);
    }

    [Fact]
    public async Task Create_upisuje_i_vraca_dto()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);

        var dto = await new CreateAnalizaHandler(t.Db).Handle(new CreateAnalizaCommand(Ulaz(p.Id, rezultat: "nijeuskladjeno")), default);

        Assert.Equal("NijeUskladjeno", dto.Rezultat);
        Assert.Equal("Stričići", dto.Selo);
        Assert.Single(t.Db.Analize);
    }

    [Fact]
    public async Task Update_prihvata_vec_vezan_proizvod_koji_je_u_medjuvremenu_obrisan()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);
        var kat = new Category { Name = "Med", Slug = "med" };
        t.Db.Categories.Add(kat);
        var pr = new Product
        {
            Name = "Med bagrem", Slug = "med-bagrem", Price = 10m, Unit = Unit.Kg, CategoryId = kat.Id,
            ProducerId = p.Id, Stock = 5, IsAvailable = true, Vrsta = VrstaProizvoda.Obican,
        };
        t.Db.Products.Add(pr);
        t.Db.SaveChanges();
        var dto = await new CreateAnalizaHandler(t.Db).Handle(new CreateAnalizaCommand(Ulaz(p.Id, pr.Id)), default);
        t.Db.ChangeTracker.Clear();
        t.Db.Products.Single(x => x.Id == pr.Id).IsDeleted = true;
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var rez = await new UpdateAnalizaHandler(t.Db).Handle(
            new UpdateAnalizaCommand(dto.Id, Ulaz(p.Id, pr.Id, uzorak: "Med livada")), default);

        Assert.Equal("Med livada", rez.Uzorak);
    }

    [Fact]
    public void Null_Input_daje_validacijsku_gresku_a_ne_NullReferenceException()
    {
        Assert.False(new CreateAnalizaValidator().Validate(new CreateAnalizaCommand(null!)).IsValid);
        Assert.False(new EPiljara.Application.Features.Partneri.PosaljiPrijavuValidator()
            .Validate(new EPiljara.Application.Features.Partneri.PosaljiPrijavuCommand(null!)).IsValid);
    }

    [Fact]
    public async Task Update_i_Delete_nepostojece_bacaju_NotFoundException()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateAnalizaHandler(t.Db).Handle(new UpdateAnalizaCommand(Guid.NewGuid(), Ulaz(p.Id)), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteAnalizaHandler(t.Db).Handle(new DeleteAnalizaCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Brisanje_proizvodjaca_sa_analizom_vraca_409_producer_in_use()
    {
        using var t = new TestDb();
        var p = NoviProizvodjac(t);
        NovaAnaliza(t, p, new DateOnly(2026, 3, 1));

        var e = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteProducerHandler(t.Db).Handle(new DeleteProducerCommand(p.Id), default));

        Assert.Equal("producer_in_use", e.Code);
    }

    [Fact]
    public void Validator_odbija_kratke_prazne_i_buduce_vrijednosti_a_prihvata_ispravan_ulaz()
    {
        var id = Guid.NewGuid();
        var v = new AnalizaInputValidator();
        Assert.False(v.Validate(Ulaz(id, uzorak: "M")).IsValid);
        Assert.False(v.Validate(Ulaz(id, laboratorija: "")).IsValid);
        Assert.False(v.Validate(Ulaz(id, ispitano: "V")).IsValid);
        Assert.False(v.Validate(Ulaz(id, datum: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3))).IsValid);
        Assert.False(v.Validate(Ulaz(id, rezultat: "99")).IsValid);
        Assert.False(v.Validate(Ulaz(id, rezultat: "nesto")).IsValid);
        Assert.True(v.Validate(Ulaz(id)).IsValid);
        Assert.True(v.Validate(Ulaz(id, rezultat: "nijeuskladjeno")).IsValid);
    }

    [Fact]
    public void Create_validator_daje_kljuceve_input_uzorak_i_input_datumUzorkovanja()
    {
        var r = new CreateAnalizaValidator().Validate(new CreateAnalizaCommand(Ulaz(
            Guid.NewGuid(), uzorak: "M", datum: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5))));

        var kljucevi = new ValidationException(r.Errors).Errors.Keys.OrderBy(k => k).ToArray();

        Assert.Equal(["input.datumUzorkovanja", "input.uzorak"], kljucevi);
    }
}
