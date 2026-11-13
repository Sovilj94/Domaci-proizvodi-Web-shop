using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/* ---------- pregled sedmice ---------- */

public record GetSadrzajPregledQuery(int Godina, int Sedmica)
    : IRequest<IReadOnlyList<SadrzajPregledDto>>;

public class GetSadrzajPregledHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSadrzajPregledQuery, IReadOnlyList<SadrzajPregledDto>>
{
    public async Task<IReadOnlyList<SadrzajPregledDto>> Handle(
        GetSadrzajPregledQuery q, CancellationToken ct)
    {
        // IsAvailable filter mora da se slaže sa GenerisiSedmicniSadrzajHandler-om (koji
        // preskače nedostupne korpice) — inače admin vidi red sa dugmetom "Generiši" koje,
        // kad se klikne, vrati praznu listu i ništa se ne desi (vidi I4, finalni review).
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .OrderBy(p => p.TipKorpice).ThenBy(p => p.Velicina)
            .ToListAsync(ct);

        var sadrzaji = await db.SedmicniSadrzaji
            .IgnoreQueryFilters()
            .Include(s => s.Stavke).ThenInclude(st => st.Product)
            .Include(s => s.Zamjene)
            .Where(s => s.Godina == q.Godina && s.Sedmica == q.Sedmica)
            .ToListAsync(ct);

        return korpice.Select(k =>
        {
            var s = sadrzaji.FirstOrDefault(x => x.KorpicaId == k.Id);
            var trosak = s is null ? null : AdminSadrzajMapiranje.Trosak(s, k.Price);
            return new SadrzajPregledDto
            {
                SadrzajId = s?.Id,
                KorpicaId = k.Id,
                KorpicaNaziv = k.Name,
                Tip = SedmicaHelper.Slug(k.TipKorpice ?? TipKorpice.Povrce),
                Velicina = (k.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
                Status = s?.Status.ToString() ?? "Nema",
                BrojArtikala = s?.Stavke.Count ?? 0,
                Vrijednost = s?.Stavke.Sum(x => x.Kolicina * x.JedinicnaCijena) ?? 0m,
                CiljGenerisanja = k.Price * opcije.Value.KoeficijentVrijednosti,
                CijenaKorpice = k.Price,
                NabavnaVrijednost = trosak?.Nabavno ?? 0m,
                Marza = trosak?.Marza,
                BezNabavneCijene = trosak?.BezNabavne ?? 0,
                BrojZamjena = s?.Zamjene.Count ?? 0,
            };
        }).ToList();
    }
}

/* ---------- jedan sadržaj za admin editor ---------- */

/// <summary>
/// Vraća sadržaj bez obzira na status — za razliku od javnog upita, koji Nacrt odbija.
/// Admin editor bi bez ovoga imao prazan ekran.
/// </summary>
public record GetSadrzajZaIzmjenuQuery(Guid Id) : IRequest<AdminSadrzajDto>;

public class GetSadrzajZaIzmjenuHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSadrzajZaIzmjenuQuery, AdminSadrzajDto>
{
    public async Task<AdminSadrzajDto> Handle(GetSadrzajZaIzmjenuQuery q, CancellationToken ct) =>
        await AdminSadrzajMapiranje.UcitajAsync(db, q.Id, opcije.Value, ct);
}

/// <summary>
/// Admin pogled na sadržaj: cijene, nabavne cijene, marža i zamjene. Nabavna se čita iz
/// TRENUTNOG kataloga — to je podatak o nabavci, ne o prodaji, isto kao proizvođač na
/// pakerskoj listi.
/// </summary>
public static class AdminSadrzajMapiranje
{
    public record TrosakSadrzaja(decimal Vrijednost, decimal Nabavno, decimal? Marza, int BezNabavne);

    /// <summary>Traži učitane <c>Stavke.Product</c>.</summary>
    public static TrosakSadrzaja Trosak(SedmicniSadrzaj s, decimal cijenaKorpice)
    {
        var vrijednost = s.Stavke.Sum(x => x.Kolicina * x.JedinicnaCijena);
        var saNabavnom = s.Stavke.Where(x => x.Product?.NabavnaCijena is > 0).ToList();
        var nabavno = saNabavnom.Sum(x => x.Kolicina * x.Product!.NabavnaCijena!.Value);
        decimal? marza = saNabavnom.Count > 0 && cijenaKorpice > 0
            ? Math.Round((cijenaKorpice - nabavno) / cijenaKorpice, 4)
            : null;
        return new TrosakSadrzaja(vrijednost, nabavno, marza, s.Stavke.Count - saNabavnom.Count);
    }

    public static async Task<AdminSadrzajDto> UcitajAsync(
        IAppDbContext db, Guid id, KorpiceOpcije o, CancellationToken ct)
    {
        var sadrzaj = await db.SedmicniSadrzaji
            .IgnoreQueryFilters()
            .Include(s => s.Korpica)
            .Include(s => s.Stavke).ThenInclude(st => st.Product).ThenInclude(p => p!.Producer)
            .Include(s => s.Zamjene).ThenInclude(z => z.Product)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        // Kutije koje su kupci već naručili za tu sedmicu: one nose SVOJ snimak sadržaja
        // (StavkaKorpiceNarudzbe), pa ih izmjena šablona ne dira — ekran to mora reći.
        var narucene = await db.OrderItems
            .Where(i => i.ProductId == sadrzaj.KorpicaId
                        && i.Order!.Godina == sadrzaj.Godina
                        && i.Order.Sedmica == sadrzaj.Sedmica
                        && i.Order.Status != OrderStatus.Cancelled)
            .SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        return sadrzaj.ToAdminDto(o, narucene) with
        {
            KategorijeIds = await KategorijeKorpiceAsync(db, sadrzaj.Korpica?.TipKorpice, ct),
        };
    }

    /// <summary>Kategorije iz kojih se puni ta vrsta korpice (povrće u korpicu povrća). Prazno = bez ograničenja.</summary>
    public static async Task<List<Guid>> KategorijeKorpiceAsync(IAppDbContext db, TipKorpice? tip, CancellationToken ct) =>
        tip is null
            ? []
            : await db.KorpicaKategorije.Where(k => k.TipKorpice == tip).Select(k => k.CategoryId).ToListAsync(ct);

    /// <summary>Traži učitane Korpica, Stavke.Product(.Producer) i Zamjene.Product.</summary>
    public static AdminSadrzajDto ToAdminDto(this SedmicniSadrzaj s, KorpiceOpcije o, int naruceneKutije = 0)
    {
        var cijena = s.Korpica?.Price ?? 0m;
        var t = Trosak(s, cijena);
        return new AdminSadrzajDto(s.ToDto())
        {
            CijenaKorpice = cijena,
            Vrijednost = t.Vrijednost,
            NabavnaVrijednost = t.Nabavno,
            Marza = t.Marza,
            MinimalnaMarza = o.MinimalnaMarza,
            NabavniBudzet = cijena * (1 - o.MinimalnaMarza),
            CiljGenerisanja = cijena * o.KoeficijentVrijednosti,
            MinimalanBrojArtikala = o.MinimalanBrojArtikala.GetValueOrDefault(s.Korpica?.Velicina ?? VelicinaKorpice.Mala, 1),
            NaruceneKutije = naruceneKutije,
            Cijene = s.Stavke.Select(st => new CijenaStavkeDto
            {
                ProductId = st.ProductId,
                JedinicnaCijena = st.JedinicnaCijena,
                NabavnaCijena = st.Product?.NabavnaCijena,
            }).ToList(),
            Zamjene = s.Zamjene
                .Where(z => z.Product is not null)
                .Select(z => new ZamjenaDto
                {
                    ProductId = z.ProductId,
                    Naziv = z.Product!.Name,
                    Jedinica = z.Product.Unit.ToApi(),
                    Cijena = z.JedinicnaCijena,
                    NabavnaCijena = z.Product.NabavnaCijena,
                    ImageUrl = z.Product.ImageUrl,
                })
                .OrderBy(z => z.Naziv)
                .ToList(),
        };
    }
}

/* ---------- izmjena sadržaja (nacrt ili objavljen) ---------- */

public record StavkaUnos(Guid ProductId, decimal Kolicina);

/// <summary>
/// Zamjenjuje stavke sadržaja u cjelosti. <c>Zamjene</c>: null = ne mijenjaj spisak zamjena
/// (osim što artikal koji je ušao u kutiju ispada iz zamjena), lista = zamijeni spisak tom listom.
/// <para>
/// <b>Nacrt</b> se mijenja slobodno do cilja generisanja; pravila (marža, broj kultura) provjerava
/// tek objava, a editor na njih upozorava uživo.
/// </para>
/// <para>
/// <b>Objavljen</b> sadržaj se od 13.11.2026. takođe može mijenjati (roba nije stigla, bolja
/// ponuda), ali mora ostati ekonomičan: pravila koja objava traži moraju i dalje važiti, a
/// <see cref="SedmicniSadrzaj.CiljnaVrijednost"/> se NE dira — to je budžet kupčevog editora i
/// svih već naručenih kutija, pa nova kutija mora vrijediti 95–100 % tog iznosa. Već naručene
/// kutije nose svoj snimak i ne mijenjaju se. <b>Zaključan</b> se ne mijenja.
/// </para>
/// </summary>
public record IzmijeniNacrtCommand(
    Guid SadrzajId, IReadOnlyList<StavkaUnos> Stavke, IReadOnlyList<Guid>? Zamjene = null)
    : IRequest<AdminSadrzajDto>;

public class IzmijeniNacrtHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<IzmijeniNacrtCommand, AdminSadrzajDto>
{
    public async Task<AdminSadrzajDto> Handle(IzmijeniNacrtCommand c, CancellationToken ct)
    {
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Include(s => s.Zamjene)
            .FirstOrDefaultAsync(s => s.Id == c.SadrzajId, ct)
            ?? throw new NotFoundException("Sadržaj korpice nije pronađen.");

        if (sadrzaj.Status == StatusSadrzaja.Zakljucan)
            throw new ConflictException(
                "Sadržaj je zaključan — rok naručivanja je prošao. Korpicu pojedinog kupca mijenjaj u Narudžbama.",
                "sadrzaj_zakljucan");
        var objavljen = sadrzaj.Status == StatusSadrzaja.Objavljen;

        // Isti ProductId dvaput u zahtjevu bi, bez ove provjere, prošao postojanje-provjeru
        // ispod (ona koristi Distinct()) ali pri insertu ostavio DVA reda za jedan artikal
        // (vidi petlju ispod koja ide kroz c.Stavke, ne kroz artikli) — duplikat koji se
        // dalje vidi kao dva reda sa istim React key-om i u javnom modalu i u editoru.
        // Odbijamo, ne spajamo tiho: spajanje bi promijenilo količinu koju je operater
        // unio bez da to bilo gdje kaže.
        if (c.Stavke.Select(s => s.ProductId).Distinct().Count() != c.Stavke.Count)
            throw new DomainException(
                "Isti artikal je dodat više puta u istom nacrtu.", "stavka_duplirana");

        var idjevi = c.Stavke.Select(s => s.ProductId).Distinct().ToList();
        var artikli = await db.Products.Where(p => idjevi.Contains(p.Id)).ToListAsync(ct);

        if (artikli.Count != idjevi.Count)
            throw new NotFoundException("Jedan ili više artikala nije pronađen.");

        if (artikli.Any(a => a.Vrsta == VrstaProizvoda.Korpica))
            throw new DomainException(
                "Korpica ne može biti stavka druge korpice.", "korpica_u_korpici");

        if (c.Stavke.Any(s => s.Kolicina <= 0))
            throw new DomainException(
                "Količina mora biti veća od nule.", "kolicina_nevalidna");

        // Artikli na komade (jaja, tegle...) se ne mogu naručiti u djelovima — "0,3 kom"
        // nema smisla. Kg/L mogu biti frakcioni. Isto pravilo kao generator
        // (KoracZaokruzivanja u GenerisiSedmicniSadrzajHandler), ovdje za ručni unos.
        if (c.Stavke.Any(s =>
                artikli.First(a => a.Id == s.ProductId).Unit == EPiljara.Domain.Enums.Unit.Kom
                && s.Kolicina != Math.Floor(s.Kolicina)))
            throw new DomainException(
                "Količina za artikal na komade mora biti cio broj.", "kolicina_mora_biti_cio_broj");

        // Nacrt se računa po katalogu (objava ionako ponovo snima cijene). U objavljenom
        // artikal koji OSTAJE zadržava cijenu snimljenu pri objavi — CiljnaVrijednost je
        // sabrana po tim cijenama, pa bi katalog pomjerio zbir a da admin ništa nije takao.
        var snimljeneCijene = sadrzaj.Stavke.ToDictionary(s => s.ProductId, s => s.JedinicnaCijena);
        decimal Cijena(Product a) =>
            objavljen && snimljeneCijene.TryGetValue(a.Id, out var snimak) ? snimak : a.Price;

        var korpica = await db.Products.IgnoreQueryFilters()
            .Where(p => p.Id == sadrzaj.KorpicaId)
            .Select(p => new { p.Name, p.Price, p.Velicina, p.TipKorpice })
            .FirstOrDefaultAsync(ct);
        var cijenaKorpice = korpica?.Price ?? 0m;

        // U korpicu povrća ulazi samo povrće: NOVI artikal mora biti iz kategorija te vrste
        // korpice (Postavke korpica). Ono što je već u kutiji ostaje i kad mu je kategorija
        // kasnije isključena — inače se takav sadržaj ne bi mogao ni popraviti.
        var kategorije = await AdminSadrzajMapiranje.KategorijeKorpiceAsync(db, korpica?.TipKorpice, ct);
        var tudji = kategorije.Count == 0
            ? null
            : artikli.FirstOrDefault(a => !snimljeneCijene.ContainsKey(a.Id) && !kategorije.Contains(a.CategoryId));
        if (tudji is not null)
            throw new DomainException(
                $"{tudji.Name} ne spada u kategorije ove korpice. Kategorije se biraju u Postavkama korpica.",
                "artikal_van_kategorija_korpice");

        var staraVrijednost = sadrzaj.Stavke.Sum(s => s.Kolicina * s.JedinicnaCijena);
        var novaVrijednost = c.Stavke.Sum(s => s.Kolicina * Cijena(artikli.First(a => a.Id == s.ProductId)));

        // Gornja granica: nacrt do cilja koji generator puni i koji ekran pokazuje
        // (CiljGenerisanja), objavljen do zamrznutog budžeta.
        GranicaKutije.Provjeri(
            staraVrijednost,
            novaVrijednost,
            objavljen ? sadrzaj.CiljnaVrijednost : cijenaKorpice * opcije.Value.KoeficijentVrijednosti);

        if (objavljen)
            await ProvjeriObjavljenAsync(sadrzaj, c, artikli, korpica?.Name, korpica?.Velicina, cijenaKorpice,
                staraVrijednost, novaVrijednost, ct);

        // Clear() je dovoljno za brisanje: StavkaSadrzaja → SedmicniSadrzaj je obavezna
        // veza (DeleteBehavior.Cascade), pa EF pri DetectChanges sam obriše osirotjele
        // stavke. Dodatni db.StavkeSadrzaja.RemoveRange(...) prije ovoga dupliciralo je
        // komandu brisanja za isti red i izazivalo DbUpdateConcurrencyException.
        sadrzaj.Stavke.Clear();

        // Nove stavke se dodaju i preko DbSet-a, ne samo preko navigacije. sadrzaj je
        // ovdje već praćen entitet (učitan upitom iznad), pa EF-ova automatska fixup-logika
        // za entitet dohvaćen samo preko sadrzaj.Stavke.Add(...) pokušava da pogodi da li je
        // nov na osnovu toga da li mu je ključ "default" — a StavkaSadrzaja.Id već ima
        // konkretan Guid (Entity.Id = Guid.NewGuid() pri konstrukciji), pa ga EF pogrešno
        // tretira kao Modified umjesto Added i generiše UPDATE za red koji ne postoji
        // (DbUpdateConcurrencyException: affected 0 row(s)). Explicit Add() to rješava.
        foreach (var unos in c.Stavke)
        {
            var artikal = artikli.First(a => a.Id == unos.ProductId);
            var stavka = new StavkaSadrzaja
            {
                ProductId = artikal.Id,
                Kolicina = unos.Kolicina,
                JedinicnaCijena = Cijena(artikal),
            };
            sadrzaj.Stavke.Add(stavka);
            db.StavkeSadrzaja.Add(stavka);
        }

        // Zamjena je po definiciji artikal VAN kutije — ono što je upravo ušlo u kutiju ispada.
        var uKutiji = idjevi.ToHashSet();
        var zamjeneIdjevi = (c.Zamjene ?? sadrzaj.Zamjene.Select(z => z.ProductId).ToList())
            .Distinct()
            .Where(id => !uKutiji.Contains(id))
            .ToList();
        var artikliZamjena = await db.Products
            .Where(p => zamjeneIdjevi.Contains(p.Id) && p.Vrsta == VrstaProizvoda.Obican)
            .ToDictionaryAsync(p => p.Id, ct);
        if (c.Zamjene is not null && artikliZamjena.Count != zamjeneIdjevi.Count)
            throw new NotFoundException("Jedna ili više zamjena nije pronađena.");

        // I predložena zamjena je iz kategorija korpice. Ona koja je već na spisku ostaje dok
        // je admin ne skine (editor ih pri otvaranju sam izbaci) — odbija se samo NOVA.
        var naSpisku = sadrzaj.Zamjene.Select(z => z.ProductId).ToHashSet();
        var tudjaZamjena = kategorije.Count == 0
            ? null
            : artikliZamjena.Values.FirstOrDefault(p => !naSpisku.Contains(p.Id) && !kategorije.Contains(p.CategoryId));
        if (tudjaZamjena is not null)
            throw new DomainException(
                $"{tudjaZamjena.Name} ne spada u kategorije ove korpice, pa ne može biti ni predložena zamjena.",
                "artikal_van_kategorija_korpice");

        // Isti obrazac kao za stavke: Clear() briše (Cascade), novi redovi idu i kroz DbSet.
        var stareCijene = sadrzaj.Zamjene.ToDictionary(z => z.ProductId, z => z.JedinicnaCijena);
        sadrzaj.Zamjene.Clear();
        foreach (var id in zamjeneIdjevi)
        {
            if (!artikliZamjena.TryGetValue(id, out var p)) continue;
            // Kupci objavljen spisak već vide: nova zamjena koju ne mogu dobiti se ne dodaje.
            if (objavljen && !stareCijene.ContainsKey(id) && !p.IsAvailable) continue;
            var z = new ZamjenaSadrzaja
            {
                ProductId = id,
                JedinicnaCijena = stareCijene.GetValueOrDefault(id, p.Price),
            };
            sadrzaj.Zamjene.Add(z);
            db.ZamjeneSadrzaja.Add(z);
        }

        await db.SaveChangesAsync(ct);
        return await AdminSadrzajMapiranje.UcitajAsync(db, sadrzaj.Id, opcije.Value, ct);
    }

    /// <summary>
    /// Objavljen sadržaj poslije izmjene mora i dalje biti ono što bi objava pustila. Stanje
    /// koje je već bilo loše (nabavna cijena je skočila poslije objave) smije da se popravlja
    /// ili ostane isto — samo ne smije da se pogorša; isto načelo kao <see cref="GranicaKutije"/>.
    /// </summary>
    private async Task ProvjeriObjavljenAsync(
        SedmicniSadrzaj sadrzaj, IzmijeniNacrtCommand c, List<Product> artikli,
        string? nazivKorpice, VelicinaKorpice? velicina, decimal cijenaKorpice,
        decimal staraVrijednost, decimal novaVrijednost, CancellationToken ct)
    {
        var stariIdjevi = sadrzaj.Stavke.Select(s => s.ProductId).ToHashSet();

        var nedostupan = artikli.FirstOrDefault(a => !stariIdjevi.Contains(a.Id) && !a.IsAvailable);
        if (nedostupan is not null)
            throw new DomainException(
                $"{nedostupan.Name} trenutno nije dostupan, pa ne može u objavljenu korpicu.",
                "sadrzaj_sadrzi_nedostupan_artikal");

        // Donja granica je kupčeva (95 % budžeta): kutija koju neko naruči ispod toga bi mu
        // pri prvoj izmjeni javila da je budžet premali, a ništa nije ni takao.
        var cilj = sadrzaj.CiljnaVrijednost;
        var najmanje = Math.Min(staraVrijednost, Math.Round(cilj * IzmijeniMojuKorpicuHandler.DonjaGranica, 2));
        if (cilj > 0m && novaVrijednost < najmanje - 0.005m)
            throw new DomainException(
                $"Korpica bi vrijedila {novaVrijednost:0.00} KM, a objavljena je sa {cilj:0.00} KM — " +
                $"smije najmanje {najmanje:0.00} KM. Povećaj količinu ili dodaj artikal za još {najmanje - novaVrijednost:0.00} KM.",
                "korpica_ispod_cilja");

        var krsi = new List<string>();

        var minimum = opcije.Value.MinimalanBrojArtikala.GetValueOrDefault(velicina ?? VelicinaKorpice.Mala, 1);
        var najmanjeArtikala = Math.Min(minimum, sadrzaj.Stavke.Count);
        if (c.Stavke.Count < najmanjeArtikala)
            krsi.Add($"ostalo bi {c.Stavke.Count} {(c.Stavke.Count == 1 ? "artikal" : "artikala")}, a najmanje je {najmanjeArtikala}");

        // Nabavna se čita iz trenutnog kataloga i za ono što izlazi (i obrisano) — isto kao
        // AdminSadrzajMapiranje.Trosak, da „prije" i „poslije" mjere isto.
        var nabavneStarih = await db.Products.IgnoreQueryFilters()
            .Where(p => stariIdjevi.Contains(p.Id))
            .Select(p => new { p.Id, p.NabavnaCijena })
            .ToDictionaryAsync(p => p.Id, p => p.NabavnaCijena, ct);
        var staraMarza = Marza(
            sadrzaj.Stavke.Select(s => (s.Kolicina, nabavneStarih.GetValueOrDefault(s.ProductId))), cijenaKorpice);
        var novaMarza = Marza(
            c.Stavke.Select(s => (s.Kolicina, artikli.First(a => a.Id == s.ProductId).NabavnaCijena)), cijenaKorpice);
        var minMarza = opcije.Value.MinimalnaMarza;
        if (novaMarza is decimal nova && nova < minMarza && (staraMarza is not decimal stara || nova < stara))
            krsi.Add($"marža bi pala na {Math.Floor(nova * 100):0} %, a najmanje je {minMarza * 100:0.#} %");

        if (krsi.Count > 0)
            throw new DomainException(
                $"{nazivKorpice ?? "Korpica"} više ne bi bila ekonomična: {string.Join("; ", krsi)}. " +
                "Izaberi jeftiniju zamjenu ili smanji količinu skupljeg artikla.",
                "sadrzaj_krsi_pravila");
    }

    /// <summary>Isti račun kao <see cref="AdminSadrzajMapiranje.Trosak"/>: null kad nijedan red nema nabavnu.</summary>
    private static decimal? Marza(IEnumerable<(decimal Kolicina, decimal? Nabavna)> redovi, decimal cijenaKorpice)
    {
        var saNabavnom = redovi.Where(r => r.Nabavna is > 0).ToList();
        if (saNabavnom.Count == 0 || cijenaKorpice <= 0m) return null;
        return Math.Round((cijenaKorpice - saNabavnom.Sum(r => r.Kolicina * r.Nabavna!.Value)) / cijenaKorpice, 4);
    }
}

/* ---------- pakerska lista ---------- */

/// <summary>Bez sedmice: zadnja zaključena — ona za koju se sad nabavlja (vidi <see cref="SedmicaHelper.ZaNabavku"/>).</summary>
public record GetPakerskaListaQuery(int? Godina = null, int? Sedmica = null) : IRequest<PakerskaListaDto>;

public class GetPakerskaListaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPakerskaListaQuery, PakerskaListaDto>
{
    public const string GrupaSpajz = "Špajz";

    public async Task<PakerskaListaDto> Handle(GetPakerskaListaQuery q, CancellationToken ct)
    {
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var (godina, sedmica) = q.Godina is int g && q.Sedmica is int s
            ? (g, s)
            : SedmicaHelper.ZaNabavku(dan, vrijemeOd, cutoff);
        var isporuka = SedmicaHelper.DatumIsporuke(godina, sedmica, dan, vrijemeOd);
        // Stanje je današnje: ima smisla porediti ga samo sa sedmicom čija isporuka tek dolazi.
        // Za isporučenu sedmicu bi „fali“ mjerio današnje zalihe naspram prošle robe.
        var porediSaStanjem = DateTime.UtcNow < isporuka;

        // Od P2b lista sabira SADRŽAJ NARUDŽBI, ne šablon. Prije toga je sabirala
        // StavkeSadrzaja — redove jedne kutije — pa je četrdeset narudžbi i jedna davale isti
        // broj, i nabavka po toj listi je bila za jednu korpicu.
        //
        // Nema IgnoreQueryFilters() ovdje: StavkaKorpiceNarudzbe, OrderItem i Order nemaju
        // globalni query filter (jedini u šemi je Product-ov !IsDeleted, AppDbContext.cs:91),
        // pa ga ovaj Include lanac (OrderItem → Order) ne dira. Zaštita za soft-obrisan
        // artikal je ispod, na upitu nad Products (vidi komentar tamo).
        //
        // Nabavlja se samo za POTVRĐENE narudžbe: na čekanju se ne broje dok ih admin ne
        // potvrdi (tada roba i nabavna vrijednost ulaze), samo se javlja koliko ih čeka.
        var redovi = await db.StavkeKorpiceNarudzbe
            .Include(s => s.OrderItem).ThenInclude(i => i!.Order)
            .Where(s => s.OrderItem!.Order!.Godina == godina
                        && s.OrderItem.Order.Sedmica == sedmica
                        && s.OrderItem.Order.Status != OrderStatus.Cancelled
                        && s.OrderItem.Order.Status != OrderStatus.Pending)
            .ToListAsync(ct);
        var naCekanju = await db.Orders.CountAsync(
            o => o.Godina == godina && o.Sedmica == sedmica && o.Status == OrderStatus.Pending, ct);

        // Artikli iz Špajza iste sedmice: redovi bez sadržaja korpice. Red same korpice se
        // izbacuje ispod (po vrsti proizvoda) — nabavlja se njen sadržaj, koji je gore.
        var spajz = await db.OrderItems
            .Where(i => i.Order!.Godina == godina
                        && i.Order.Sedmica == sedmica
                        && i.Order.Status != OrderStatus.Cancelled
                        && i.Order.Status != OrderStatus.Pending
                        && !i.KorpicaStavke.Any())
            .Select(i => new { i.OrderId, i.ProductId, i.ProductName, i.Unit, i.Quantity })
            .ToListAsync(ct);

        // Artikal se dohvata posebno, jer StavkaKorpiceNarudzbe namjerno nema navigaciju na
        // Product — snapshot naziva živi na samom redu, a proizvođač i selo se čitaju iz
        // trenutnog kataloga jer su podatak o nabavci, ne o prodaji.
        var idjevi = redovi.Select(s => s.ProductId).Concat(spajz.Select(s => s.ProductId)).Distinct().ToList();
        var artikli = await db.Products
            .IgnoreQueryFilters()
            .Include(p => p.Producer)
            .Include(p => p.Category)
            .Where(p => idjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        // Kategorija koja puni neku korpicu je svoja grupa nabavke; redoslijed je po prvom
        // tipu korpice koji puni (Povrće, Voće, Mliječna…). Sve ostalo je Špajz, na kraju.
        var redKategorije = (await db.KorpicaKategorije.ToListAsync(ct))
            .GroupBy(k => k.CategoryId)
            .ToDictionary(g => g.Key, g => g.Min(k => (int)k.TipKorpice));
        spajz = spajz
            .Where(s => !artikli.TryGetValue(s.ProductId, out var p) || p.Vrsta != VrstaProizvoda.Korpica)
            .ToList();

        var brojNarudzbi = redovi.Select(s => s.OrderItem!.OrderId)
            .Concat(spajz.Select(s => s.OrderId))
            .Distinct()
            .Count();

        var uKorpicama = redovi.GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Kolicina * x.OrderItem!.Quantity));
        var uSpajzu = spajz.GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var snapshot = redovi.Select(r => (r.ProductId, r.ProductName, r.Unit))
            .Concat(spajz.Select(s => (s.ProductId, s.ProductName, s.Unit)))
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        var stavke = uKorpicama.Keys.Union(uSpajzu.Keys)
            .Select(id =>
            {
                artikli.TryGetValue(id, out var p);
                var prvi = snapshot[id];
                var korpice = uKorpicama.GetValueOrDefault(id);
                // Stock je već umanjen za Špajz (skida se pri narudžbi), a za sadržaj korpica
                // nije (vidi Zalihe) — pa nabaviti treba samo ono što korpice traže preko stanja.
                var naStanju = p is null ? 0m : Math.Max(0m, p.Stock);
                var kategorija = p?.Category?.Name ?? string.Empty;
                var uKorpici = p is not null && redKategorije.ContainsKey(p.CategoryId);
                return new StavkaPakerskeDto
                {
                    Artikal = p?.Name ?? prvi.ProductName,
                    Kategorija = kategorija,
                    Grupa = uKorpici ? kategorija : GrupaSpajz,
                    RedGrupe = uKorpici ? redKategorije[p!.CategoryId] : int.MaxValue,
                    Kolicina = korpice + uSpajzu.GetValueOrDefault(id),
                    UKorpicama = korpice,
                    USpajzu = uSpajzu.GetValueOrDefault(id),
                    NaStanju = naStanju,
                    Fali = porediSaStanjem ? Math.Max(0m, korpice - naStanju) : 0m,
                    Jedinica = (p?.Unit ?? prvi.Unit).ToApi(),
                    Proizvodjac = p?.Producer?.Name ?? string.Empty,
                    Selo = p?.Producer?.Village ?? string.Empty,
                    Telefon = p?.Producer?.Phone,
                    NabavnaCijena = p?.NabavnaCijena,
                    ArtikalObrisan = p is null || p.IsDeleted,
                };
            })
            .OrderBy(r => r.Proizvodjac).ThenBy(r => r.Artikal)
            .ToList();

        var rok = isporuka.AddHours(-cutoff);

        return new PakerskaListaDto
        {
            Godina = godina,
            Sedmica = sedmica,
            BrojNarudzbi = brojNarudzbi,
            NaCekanju = naCekanju,
            Rok = rok,
            Isporuka = isporuka,
            Zakljucena = DateTime.UtcNow > rok,
            Stavke = stavke,
            NabavnaVrijednost = Math.Round(
                stavke.Where(s => s.NabavnaCijena is > 0).Sum(s => s.Kolicina * s.NabavnaCijena!.Value), 2),
            BezNabavneCijene = stavke.Count(s => s.NabavnaCijena is not > 0),
        };
    }
}
