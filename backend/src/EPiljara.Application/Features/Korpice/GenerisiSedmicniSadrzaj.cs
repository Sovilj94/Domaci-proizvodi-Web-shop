using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

public record RezultatGenerisanjaDto
{
    public Guid SadrzajId { get; init; }
    public Guid KorpicaId { get; init; }
    public string KorpicaNaziv { get; init; } = string.Empty;
    public int BrojArtikala { get; init; }
    public decimal Vrijednost { get; init; }
    public decimal CiljGenerisanja { get; init; }

    /// <summary>Σ(količina × nabavna cijena) za artikle kojima je nabavna cijena unesena.</summary>
    public decimal NabavnaVrijednost { get; init; }

    /// <summary>Marža korpice (0–1) prema njenoj cijeni. Null kad nijedan artikal nema nabavnu cijenu.</summary>
    public decimal? Marza { get; init; }

    /// <summary>Artikli u sadržaju bez nabavne cijene — za njih marža nije provjerena.</summary>
    public IReadOnlyList<string> BezNabavneCijene { get; init; } = [];

    public int BrojZamjena { get; init; }

    /// <summary>Popunjeno kad bazen nije mogao ispuniti minimum. Nije greška — admin dorađuje.</summary>
    public string? Upozorenje { get; init; }
}

/// <summary>
/// Sastavlja PRIJEDLOG sadržaja za zadatu sedmicu. Rezultat je uvijek Nacrt —
/// admin je posljednja instanca. Korpice koje za tu sedmicu već imaju sadržaj se preskaču,
/// osim kad je <paramref name="Ponovo"/> postavljen: tada se postojeći NACRT briše i
/// sastavlja iznova (objavljen sadržaj se nikad ne dira — kupci su ga već vidjeli).
/// </summary>
public record GenerisiSedmicniSadrzajCommand(int Godina, int Sedmica, Guid? KorpicaId, bool Ponovo = false)
    : IRequest<IReadOnlyList<RezultatGenerisanjaDto>>;

public class GenerisiSedmicniSadrzajHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GenerisiSedmicniSadrzajCommand, IReadOnlyList<RezultatGenerisanjaDto>>
{
    private readonly KorpiceOpcije _o = opcije.Value;

    public async Task<IReadOnlyList<RezultatGenerisanjaDto>> Handle(
        GenerisiSedmicniSadrzajCommand c, CancellationToken ct)
    {
        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .Where(p => c.KorpicaId == null || p.Id == c.KorpicaId)
            .ToListAsync(ct);

        var postojeci = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Include(s => s.Zamjene)
            .Where(s => s.Godina == c.Godina && s.Sedmica == c.Sedmica)
            .ToListAsync(ct);

        if (c.Ponovo)
        {
            var nacrti = postojeci
                .Where(s => s.Status == StatusSadrzaja.Nacrt
                            && korpice.Any(k => k.Id == s.KorpicaId))
                .ToList();
            if (nacrti.Count > 0)
            {
                db.SedmicniSadrzaji.RemoveRange(nacrti);
                // Brisanje mora otići prije inserta: (KorpicaId, Godina, Sedmica) je jedinstven.
                await db.SaveChangesAsync(ct);
                postojeci = postojeci.Except(nacrti).ToList();
            }
        }

        var vecPostoje = postojeci.Select(s => s.KorpicaId).ToHashSet();

        var veze = await db.KorpicaKategorije.ToListAsync(ct);
        var mjesec = MjesecSedmice(c.Godina, c.Sedmica);
        var rezultati = new List<RezultatGenerisanjaDto>();

        // Cijela sezonska ponuda — rezerva za zamjene kad kategorije korpice sve potroše u kutiju.
        var sezonskaPonuda = KorpicaPodobnost.USezoni(
            await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec).ToList();

        foreach (var korpica in korpice.Where(k => !vecPostoje.Contains(k.Id)))
        {
            var kategorije = veze
                .Where(v => v.TipKorpice == korpica.TipKorpice)
                .Select(v => v.CategoryId)
                .ToHashSet();

            // Mjeseci su CSV u bazi, pa se sezona filtrira u memoriji nakon učitavanja.
            // Uslov podobnosti dijeli sa kupčevim editorom — vidi KorpicaPodobnost.
            // Ograničenje na kategorije te korpice ostaje SAMO ovdje: generator puni povrćnu
            // korpicu povrćem, a kupac po odluci iz P2b bira iz cijele ponude.
            //
            // Unutar istog prioriteta prednost imaju artikli sa boljom maržom (u razredima od
            // 10%, pa unutar razreda nasumično — da se sadržaj mijenja iz sedmice u sedmicu).
            // Artikal bez nabavne cijene ide iza onih sa njom.
            var bazen = KorpicaPodobnost.USezoni(
                    await KorpicaPodobnost.UpitPodobnih(db.Products)
                        .Where(p => kategorije.Contains(p.CategoryId))
                        .ToListAsync(ct),
                    mjesec)
                .OrderBy(p => p.PrioritetUKorpici)
                .ThenByDescending(RazredMarze)
                .ThenBy(_ => Guid.NewGuid())
                .ToList();

            var minimum = _o.MinimalanBrojArtikala.GetValueOrDefault(
                korpica.Velicina ?? VelicinaKorpice.Mala, 5);
            var cilj = korpica.Price * _o.KoeficijentVrijednosti;

            // Koliko sadržaj smije koštati u nabavci da korpica zadrži minimalnu maržu.
            // Važi samo za artikle sa unesenom nabavnom cijenom; ostali se javljaju adminu.
            var nabavniBudzet = korpica.Price * (1 - _o.MinimalnaMarza);

            // Budžet po stavci dok se ne ispuni minimum. Bez ovoga prvi artikal
            // pojede cio cilj i korpica ispadne sa jednom stavkom.
            var poStavci = cilj / Math.Max(minimum, 1);

            var sadrzaj = new SedmicniSadrzaj
            {
                KorpicaId = korpica.Id,
                Godina = c.Godina,
                Sedmica = c.Sedmica,
                Status = StatusSadrzaja.Nacrt,
            };

            var vrijednost = 0m;
            var nabavno = 0m;
            var nabavnoOgranicilo = false;
            var bezNabavne = new List<string>();

            foreach (var artikal in bazen)
            {
                if (vrijednost >= cilj * 0.95m && sadrzaj.Stavke.Count >= minimum) break;
                if (artikal.Price <= 0) continue;

                var preostalo = cilj - vrijednost;
                if (preostalo <= 0) break;

                var budzet = sadrzaj.Stavke.Count < minimum
                    ? Math.Min(poStavci, preostalo)
                    : preostalo;

                var kolicina = Zaokruzi(budzet / artikal.Price, artikal.Unit);
                var nab = artikal.NabavnaCijena is > 0 ? artikal.NabavnaCijena.Value : (decimal?)null;
                if (nab is not null)
                {
                    var poNabavci = Zaokruzi(Math.Max(nabavniBudzet - nabavno, 0) / nab.Value, artikal.Unit);
                    if (poNabavci < kolicina)
                    {
                        kolicina = poNabavci;
                        nabavnoOgranicilo = true;
                    }
                }
                if (kolicina > artikal.Stock) kolicina = Zaokruzi(artikal.Stock, artikal.Unit);

                if (kolicina <= 0)
                {
                    // Budžet po stavci je zaokružen na 0 — tipično skup artikal po komadu
                    // (suhomesnato, mliječno) naspram malog poStavci. Umjesto da ga trajno
                    // preskočimo, probamo tačno jednu minimalnu jedinicu, ali samo ako staje
                    // u preostatak cilja, u zalihu i u nabavni budžet; inače ostaje preskočen.
                    var minimalnaKolicina = KoracZaokruzivanja(artikal.Unit);
                    if (minimalnaKolicina * artikal.Price <= preostalo
                        && minimalnaKolicina <= artikal.Stock
                        && (nab is null || nabavno + minimalnaKolicina * nab.Value <= nabavniBudzet))
                        kolicina = minimalnaKolicina;
                    else
                        continue;
                }

                sadrzaj.Stavke.Add(new StavkaSadrzaja
                {
                    ProductId = artikal.Id,
                    Kolicina = kolicina,
                    JedinicnaCijena = artikal.Price,
                });
                vrijednost += kolicina * artikal.Price;
                if (nab is null) bezNabavne.Add(artikal.Name);
                else nabavno += kolicina * nab.Value;
            }

            // Drugi prolaz: bazen je potrošen, a vrijednost još ispod cilja (tipično zimi, kad u
            // bazenu ima tek koji artikal preko minimuma, pa budžet po stavci i zaokruživanje
            // komada ostave rupu). Količine onoga što je već u kutiji se povećaju istim redom,
            // u granicama zalihe i nabavnog budžeta.
            foreach (var stavka in sadrzaj.Stavke)
            {
                if (vrijednost >= cilj * 0.95m) break;
                var artikal = bazen.First(p => p.Id == stavka.ProductId);
                var dodatak = Zaokruzi((cilj - vrijednost) / artikal.Price, artikal.Unit);
                dodatak = Math.Min(dodatak, Zaokruzi(artikal.Stock - stavka.Kolicina, artikal.Unit));
                var nab = artikal.NabavnaCijena is > 0 ? artikal.NabavnaCijena.Value : (decimal?)null;
                if (nab is not null)
                    dodatak = Math.Min(dodatak, Zaokruzi(Math.Max(nabavniBudzet - nabavno, 0) / nab.Value, artikal.Unit));
                if (dodatak <= 0) continue;

                stavka.Kolicina += dodatak;
                vrijednost += dodatak * artikal.Price;
                if (nab is not null) nabavno += dodatak * nab.Value;
            }

            DodajZamjene(sadrzaj, bazen, sezonskaPonuda, cilj, nabavniBudzet);
            db.SedmicniSadrzaji.Add(sadrzaj);

            var upozorenja = new List<string>();
            if (sadrzaj.Stavke.Count < minimum)
                upozorenja.Add($"U bazenu je premalo artikala: {sadrzaj.Stavke.Count} od {minimum}. Dopuni sadržaj ručno.");
            if (nabavnoOgranicilo && vrijednost < cilj * 0.95m)
                upozorenja.Add(
                    $"Nabavni budžet ({nabavniBudzet:0.##} KM za maržu {_o.MinimalnaMarza * 100:0.#}%) ograničio je sadržaj " +
                    $"na {vrijednost:0.##} KM od cilja {cilj:0.##} KM. Dodaj artikle sa boljom maržom ili podigni cijenu korpice.");

            var imaNabavnih = sadrzaj.Stavke.Count > bezNabavne.Count;
            rezultati.Add(new RezultatGenerisanjaDto
            {
                SadrzajId = sadrzaj.Id,
                KorpicaId = korpica.Id,
                KorpicaNaziv = korpica.Name,
                BrojArtikala = sadrzaj.Stavke.Count,
                Vrijednost = vrijednost,
                CiljGenerisanja = cilj,
                NabavnaVrijednost = nabavno,
                Marza = imaNabavnih && korpica.Price > 0
                    ? Math.Round((korpica.Price - nabavno) / korpica.Price, 4)
                    : null,
                BezNabavneCijene = bezNabavne,
                BrojZamjena = sadrzaj.Zamjene.Count,
                Upozorenje = upozorenja.Count > 0 ? string.Join(" ", upozorenja) : null,
            });
        }

        await db.SaveChangesAsync(ct);
        return rezultati;
    }

    /// <summary>
    /// Bira predložene zamjene: artikle iz bazena koji nisu u kutiji. Kupac jedan artikal
    /// mijenja za drugi iste vrijednosti, pa zamjena ne smije imati lošiji odnos nabavne i
    /// prodajne cijene od onog koji drži cijelu kutiju u marži (nabavni budžet / cilj) —
    /// inače bi svaka zamjena kutiju gurala ispod marže. Zamjena bez nabavne cijene se
    /// dozvoljava (ne zna se), ali ide na kraj.
    ///
    /// Prvo se biraju artikli iz kategorija korpice; kad ih kutija sve potroši (mali sezonski
    /// bazen, npr. povrće u novembru), dopunjava se iz ostale sezonske ponude — kupac i tako
    /// smije birati iz cijele ponude (odluka P2b).
    /// </summary>
    private void DodajZamjene(
        SedmicniSadrzaj sadrzaj, List<Product> bazen, List<Product> sezonskaPonuda,
        decimal cilj, decimal nabavniBudzet)
    {
        if (_o.BrojZamjena <= 0 || cilj <= 0) return;

        var najveciOdnos = nabavniBudzet / cilj;
        var uKutiji = sadrzaj.Stavke.Select(s => s.ProductId).ToHashSet();
        var izBazena = bazen.Select(p => p.Id).ToHashSet();

        IEnumerable<Product> Kandidati(IEnumerable<Product> izvor) => izvor
            .Where(p => !uKutiji.Contains(p.Id) && p.Price > 0)
            .Where(p => p.NabavnaCijena is not > 0 || p.NabavnaCijena.Value / p.Price <= najveciOdnos)
            .OrderByDescending(RazredMarze)
            .ThenBy(_ => Guid.NewGuid());

        var izbor = Kandidati(bazen)
            .Concat(Kandidati(sezonskaPonuda.Where(p => !izBazena.Contains(p.Id))))
            .Take(_o.BrojZamjena);

        foreach (var p in izbor)
            sadrzaj.Zamjene.Add(new ZamjenaSadrzaja { ProductId = p.Id, JedinicnaCijena = p.Price });
    }

    /// <summary>Marža u razredima od 10% (0–10); −1 kad nabavna cijena nije unesena.</summary>
    private static int RazredMarze(Product p) =>
        p.NabavnaCijena is > 0 && p.Price > 0
            ? (int)Math.Floor((p.Price - p.NabavnaCijena.Value) / p.Price * 10)
            : -1;

    /// <summary>
    /// Korak zaokruživanja (i najmanja smislena količina): 1 za komade, 0.1 za težinu i
    /// zapreminu. Tip parametra je kvalifikovan jer se <c>Unit</c> inače sudara sa
    /// <c>MediatR.Unit</c> (oba imena su importovana u ovom fajlu).
    /// </summary>
    internal static decimal KoracZaokruzivanja(EPiljara.Domain.Enums.Unit jedinica) =>
        jedinica == EPiljara.Domain.Enums.Unit.Kom ? 1m : 0.1m;

    internal static decimal Zaokruzi(decimal kolicina, EPiljara.Domain.Enums.Unit jedinica)
    {
        var korak = KoracZaokruzivanja(jedinica);
        return Math.Floor(kolicina / korak) * korak;
    }

    /// <summary>Mjesec u koji pada četvrtak zadate ISO sedmice — četvrtak je uvijek u „pravom" mjesecu.</summary>
    private static int MjesecSedmice(int godina, int sedmica) =>
        ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).Month;
}
