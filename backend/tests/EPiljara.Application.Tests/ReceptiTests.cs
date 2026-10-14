using EPiljara.Application.Common.Exceptions;
using EPiljara.Application.Features.Recepti;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Recepti. Javno smiju izaći samo objavljeni, a sastojak smije nuditi samo običan,
/// neobrisan proizvod; admin Update mora zamijeniti sastojke, ne ostaviti siročad.
/// </summary>
public class ReceptiTests
{
    private static (Product obican, Product nedostupan, Product korpica) Postavi(TestDb t)
    {
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proiz = new Producer { Name = "Porodica Kovačević", Village = "Stričići" };
        t.Db.Categories.Add(kat);
        t.Db.Producers.Add(proiz);

        Product Novi(string ime, string slug, VrstaProizvoda vrsta, bool dostupan) => new()
        {
            Name = ime, Slug = slug, Price = 3m, Unit = Unit.Kg, CategoryId = kat.Id,
            ProducerId = proiz.Id, Stock = 20, IsAvailable = dostupan, Vrsta = vrsta,
            TipKorpice = vrsta == VrstaProizvoda.Korpica ? TipKorpice.Domacinska : null,
            Velicina = vrsta == VrstaProizvoda.Korpica ? VelicinaKorpice.Mala : null,
        };
        var obican = Novi("Babura paprika", "babura-paprika", VrstaProizvoda.Obican, true);
        var nedostupan = Novi("Crni luk", "crni-luk", VrstaProizvoda.Obican, false);
        var korpica = Novi("Mala korpica", "mala-korpica", VrstaProizvoda.Korpica, true);
        t.Db.Products.AddRange(obican, nedostupan, korpica);
        t.Db.SaveChanges();
        return (obican, nedostupan, korpica);
    }

    private static Recept NoviRecept(TestDb t, string naslov, bool objavljen = true,
        DateTime? kreiran = null, params ReceptSastojak[] sastojci)
    {
        var r = new Recept
        {
            Naslov = naslov, Slug = naslov.ToLowerInvariant().Replace(' ', '-'),
            Opis = "Opis recepta.", VrijemeMinuta = 20, Porcije = 2, Tezina = TezinaRecepta.Lako,
            Koraci = "1. Opere se.\n2. Isječe se.", Objavljen = objavljen,
            Kreiran = kreiran ?? DateTime.UtcNow, Sastojci = sastojci.ToList(),
        };
        t.Db.Recepti.Add(r);
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();
        return r;
    }

    private static ReceptInput Ulaz(string naslov = "Kajgana sa sirom", string koraci = "1. Razbije se jaje.\n2. Ispeče se.",
        IReadOnlyList<SastojakInput>? sastojci = null, int vrijeme = 15, int porcije = 2, string tezina = "Lako") =>
        new(naslov, "Brz doručak.", null, vrijeme, porcije, tezina, koraci, true, sastojci);

    [Fact]
    public async Task Javna_lista_vraca_samo_objavljene_najnoviji_prvi()
    {
        using var t = new TestDb();
        var sada = DateTime.UtcNow;
        NoviRecept(t, "Stariji", true, sada.AddDays(-2));
        NoviRecept(t, "Skriveni", false, sada);
        NoviRecept(t, "Noviji", true, sada.AddDays(-1));

        var rez = await new GetReceptiHandler(t.Db).Handle(new GetReceptiQuery(), default);

        Assert.Equal(["Noviji", "Stariji"], rez.Select(r => r.Naslov));
        Assert.Equal("Lako", rez[0].Tezina);
    }

    [Fact]
    public async Task Javni_detalj_neobjavljenog_ili_nepostojeceg_je_404()
    {
        using var t = new TestDb();
        var skriven = NoviRecept(t, "Skriven", false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetReceptBySlugHandler(t.Db).Handle(new GetReceptBySlugQuery(skriven.Slug), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetReceptBySlugHandler(t.Db).Handle(new GetReceptBySlugQuery("nema-ga"), default));
    }

    [Fact]
    public async Task Detalj_nosi_proizvod_samo_za_obican_neobrisan_artikal_po_redoslijedu()
    {
        using var t = new TestDb();
        var (obican, nedostupan, korpica) = Postavi(t);
        var obrisan = new Product
        {
            Name = "Obrisana mrkva", Slug = "obrisana-mrkva", Price = 2m, Unit = Unit.Kg,
            CategoryId = obican.CategoryId, ProducerId = obican.ProducerId, Stock = 5,
            Vrsta = VrstaProizvoda.Obican, IsDeleted = true,
        };
        t.Db.Products.Add(obrisan);
        t.Db.SaveChanges();
        var r = NoviRecept(t, "Salata", true, null,
            new ReceptSastojak { Naziv = "Obrisan", Kolicina = "1", ProductId = obrisan.Id, Redoslijed = 4 },
            new ReceptSastojak { Naziv = "Bez veze", Kolicina = "po ukusu", Redoslijed = 5 },
            new ReceptSastojak { Naziv = "Korpica", Kolicina = "1", ProductId = korpica.Id, Redoslijed = 3 },
            new ReceptSastojak { Naziv = "Nedostupan", Kolicina = "1", ProductId = nedostupan.Id, Redoslijed = 2 },
            new ReceptSastojak { Naziv = "Paprika", Kolicina = "2 kom", ProductId = obican.Id, Redoslijed = 1 });

        var dto = await new GetReceptBySlugHandler(t.Db).Handle(new GetReceptBySlugQuery(r.Slug), default);

        Assert.Equal(["Paprika", "Nedostupan", "Korpica", "Obrisan", "Bez veze"], dto.Sastojci.Select(s => s.Naziv));
        Assert.Equal("babura-paprika", dto.Sastojci[0].Proizvod?.Slug);
        Assert.True(dto.Sastojci[0].Proizvod!.IsAvailable);
        // Nedostupan običan proizvod se vraća — UI odlučuje o dugmetu.
        Assert.False(dto.Sastojci[1].Proizvod!.IsAvailable);
        Assert.Null(dto.Sastojci[2].Proizvod);   // korpica
        Assert.Null(dto.Sastojci[3].Proizvod);   // obrisan
        Assert.Null(dto.Sastojci[4].Proizvod);   // bez veze
    }

    [Fact]
    public async Task Koraci_se_razbijaju_po_redovima_bez_praznih()
    {
        using var t = new TestDb();
        var r = NoviRecept(t, "Koraci");
        t.Db.Recepti.Single(x => x.Id == r.Id).Koraci = "1. Opere se.\n\n2. Isječe se.\r\n";
        t.Db.SaveChanges();
        t.Db.ChangeTracker.Clear();

        var dto = await new GetReceptBySlugHandler(t.Db).Handle(new GetReceptBySlugQuery(r.Slug), default);

        Assert.Equal(["1. Opere se.", "2. Isječe se."], dto.Koraci);
    }

    [Fact]
    public async Task Create_pravi_slug_iz_naslova_a_drugi_istog_naslova_dobija_sufiks()
    {
        using var t = new TestDb();
        var h = new CreateReceptHandler(t.Db);

        var prvi = await h.Handle(new CreateReceptCommand(Ulaz("Kajgana sa sirom")), default);
        var drugi = await h.Handle(new CreateReceptCommand(Ulaz("Kajgana sa sirom")), default);

        Assert.Equal("kajgana-sa-sirom", prvi.Slug);
        Assert.Equal("kajgana-sa-sirom-2", drugi.Slug);
    }

    [Fact]
    public async Task Create_sa_nepostojecim_proizvodom_daje_proizvod_ne_postoji()
    {
        using var t = new TestDb();
        var ulaz = Ulaz(sastojci: [new SastojakInput("Sir", "100 g", Guid.NewGuid())]);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            new CreateReceptHandler(t.Db).Handle(new CreateReceptCommand(ulaz), default));

        Assert.Equal("proizvod_ne_postoji", ex.Code);
        Assert.Empty(t.Db.Recepti);
    }

    [Fact]
    public async Task Update_zamjenjuje_sastojke_bez_siroceta_i_ne_mijenja_slug()
    {
        using var t = new TestDb();
        var (obican, _, _) = Postavi(t);
        var kreiran = await new CreateReceptHandler(t.Db).Handle(new CreateReceptCommand(Ulaz(
            sastojci:
            [
                new SastojakInput("Jaja", "4 kom", null),
                new SastojakInput("Paprika", "1 kom", obican.Id),
                new SastojakInput("So", "", null),
            ])), default);
        t.Db.ChangeTracker.Clear();
        Assert.Equal(3, t.Db.ReceptSastojci.Count(s => s.ReceptId == kreiran.Id));

        var rez = await new UpdateReceptHandler(t.Db).Handle(new UpdateReceptCommand(kreiran.Id, Ulaz(
            naslov: "Potpuno drugi naslov",
            sastojci: [new SastojakInput("Sir", "100 g", null), new SastojakInput("Paprika", "2 kom", obican.Id)])),
            default);
        t.Db.ChangeTracker.Clear();

        Assert.Equal(2, t.Db.ReceptSastojci.Count(s => s.ReceptId == kreiran.Id));
        Assert.Equal(2, t.Db.ReceptSastojci.Count());
        var iz = t.Db.Recepti.Include(r => r.Sastojci).Single();
        Assert.Equal("Potpuno drugi naslov", iz.Naslov);
        Assert.Equal("kajgana-sa-sirom", iz.Slug);
        Assert.Equal(["Sir", "Paprika"], iz.Sastojci.OrderBy(s => s.Redoslijed).Select(s => s.Naziv));
        Assert.Equal(["Sir", "Paprika"], rez.Sastojci.Select(s => s.Naziv));
    }

    [Fact]
    public async Task Update_i_delete_nepostojeceg_daju_404()
    {
        using var t = new TestDb();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateReceptHandler(t.Db).Handle(new UpdateReceptCommand(id, Ulaz()), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new DeleteReceptHandler(t.Db).Handle(new DeleteReceptCommand(id), default));
    }

    [Fact]
    public async Task Delete_brise_i_sastojke()
    {
        using var t = new TestDb();
        var kreiran = await new CreateReceptHandler(t.Db).Handle(new CreateReceptCommand(Ulaz(
            sastojci: [new SastojakInput("Jaja", "4 kom", null), new SastojakInput("So", "", null)])), default);
        t.Db.ChangeTracker.Clear();

        await new DeleteReceptHandler(t.Db).Handle(new DeleteReceptCommand(kreiran.Id), default);
        t.Db.ChangeTracker.Clear();

        Assert.Empty(t.Db.Recepti);
        Assert.Empty(t.Db.ReceptSastojci);
    }

    [Theory]
    [InlineData("Ka", "1. Razbije se jaje.", 15, 2, "Lako", "naslov kraći od 3")]
    [InlineData("Kajgana", "1. Razbije se jaje.", 0, 2, "Lako", "vrijeme 0")]
    [InlineData("Kajgana", "1. Razbije se jaje.", 15, 21, "Lako", "porcije 21")]
    [InlineData("Kajgana", "Kratko", 15, 2, "Lako", "koraci kraći od 10")]
    [InlineData("Kajgana", "1. Razbije se jaje.", 15, 2, "Nemoguce", "nepoznata težina")]
    public void Validator_odbija(string naslov, string koraci, int vrijeme, int porcije, string tezina, string razlog)
    {
        var r = new ReceptInputValidator().Validate(Ulaz(naslov, koraci, null, vrijeme, porcije, tezina));
        Assert.False(r.IsValid, razlog);
    }

    [Fact]
    public void Validator_odbija_sastojak_sa_praznim_nazivom()
    {
        var r = new ReceptInputValidator().Validate(Ulaz(sastojci: [new SastojakInput("   ", "1 kom", null)]));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validator_prihvata_ispravan_ulaz_bez_sastojaka_i_tezinu_bilo_kojim_slovima()
    {
        Assert.True(new ReceptInputValidator().Validate(Ulaz(sastojci: null)).IsValid);
        Assert.True(new ReceptInputValidator().Validate(Ulaz(tezina: "zahtjevno")).IsValid);
    }

    [Fact]
    public void Create_validator_daje_kljuceve_input_naslov_i_input_sastojci_0_naziv()
    {
        // Ključeve čita admin forma (errors["input.naslov"] …).
        var r = new CreateReceptValidator().Validate(new CreateReceptCommand(Ulaz(
            naslov: "K", sastojci: [new SastojakInput("", "1 kom", null)])));

        var kljucevi = new ValidationException(r.Errors).Errors.Keys.OrderBy(k => k).ToArray();

        Assert.Equal(["input.naslov", "input.sastojci[0].naziv"], kljucevi);
    }
}
