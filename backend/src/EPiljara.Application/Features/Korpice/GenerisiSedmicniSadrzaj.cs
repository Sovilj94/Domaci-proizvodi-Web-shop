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

/// <summary>
/// Kako se sastavlja korpica (pravila, redom):
/// <list type="number">
/// <item>Bazen: artikli iz kategorija te vrste korpice, u sezoni, smiju u korpicu, na stanju, nisu opcioni.</item>
/// <item>Redoslijed (šta je ljudima najpotrebnije): prioritet u korpici (0 = osnovna namirnica za
///   trpezu — krompir, luk, mrkva…; admin ga postavlja u Postavkama korpica), unutar njega glavne
///   kulture mjeseca, pa stvarna potražnja iz narudžbi zadnjih 12 sedmica (kupljeno samo i dodato u korpicu, minus
///   kupci koji artikal izbjegavaju), pa ono čega nije bilo prošle sedmice, pa marža, pa nasumično.</item>
/// <item>Raznolikost: druga vrsta iste namirnice (crveni pored domaćeg krompira) ide tek kad drugih nema.</item>
/// <item>Izbor: prvo se bira tačno <c>MinimalanBrojArtikala</c> kultura sa NAJMANJOM smislenom količinom
///   (pola zadate, kao u adminovom editoru), i artikal ulazi samo ako iza njega u cilju vrijednosti i
///   nabavnom budžetu ostaje mjesta za preostale kulture. Bez ove rezerve su prvi (skuplji) artikli
///   pojeli budžet i korpica je ostajala na 6 kultura umjesto 8.</item>
/// <item>Količine: tek kad je broj kultura siguran, količine se dižu do zadate, redom po važnosti, u
///   granicama cilja vrijednosti, nabavnog budžeta (najmanja marža) i zalihe. Ako je i dalje ispod
///   95 % cilja, dodaje se još koja kultura, a artikli bez zadate količine smiju i preko nje.</item>
/// <item>Težina (samo korpice sa rasponom, Product.TezinaOd/TezinaDo): sadržaj ne prelazi najveću
///   težinu, jedna kultura ne zauzima više od četvrtine nje (osnovna namirnica do pola), a ispod
///   najmanje težine se količine dižu redom po važnosti. Komad se mjeri preko Product.TezinaKomada.</item>
/// </list>
/// </summary>
public class GenerisiSedmicniSadrzajHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GenerisiSedmicniSadrzajCommand, IReadOnlyList<RezultatGenerisanjaDto>>
{
    private readonly KorpiceOpcije _o = opcije.Value;

    /// <summary>Koliko sedmica unazad se gleda potražnja.</summary>
    private const int SedmicaPotraznje = 12;

    /// <summary>Najviše koliko kultura preko minimuma generator dodaje da bi dosegao cilj vrijednosti.</summary>
    private const int NajviseDodatnih = 2;

    private const decimal Tolerancija = 0.005m;

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

        // Mjeseci su CSV u bazi, pa se sezona filtrira u memoriji nakon učitavanja (KorpicaPodobnost).
        var sezonskaPonuda = KorpicaPodobnost.USezoni(
            await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec).ToList();

        var potraznja = await PotraznjaAsync(c.Godina, c.Sedmica, ct);
        var proslaSedmica = await ProslaSedmicaAsync(c.Godina, c.Sedmica, ct);

        foreach (var korpica in korpice.Where(k => !vecPostoje.Contains(k.Id)))
        {
            var kategorije = veze
                .Where(v => v.TipKorpice == korpica.TipKorpice)
                .Select(v => v.CategoryId)
                .ToHashSet();

            // Ograničenje na kategorije te korpice ostaje SAMO ovdje: generator puni povrćnu
            // korpicu povrćem, a kupac po odluci iz P2b bira iz cijele ponude.
            var bila = proslaSedmica.GetValueOrDefault(korpica.Id) ?? [];
            // Osnova trpeze (prioritet 0: krompir, luk, mrkva) ide PRIJE glavnih kultura mjeseca —
            // inače sezona (cvekla, bundeva) istisne krompir iz male korpice. Glavne su prve unutar
            // istog prioriteta.
            var bazen = sezonskaPonuda
                .Where(p => kategorije.Contains(p.CategoryId))
                .OrderBy(p => p.PrioritetUKorpici)
                .ThenByDescending(p => p.GlavnoU(mjesec))
                .ThenByDescending(p => RazredPotraznje(potraznja.GetValueOrDefault(p.Id)))
                .ThenBy(p => bila.Contains(p.Id))
                .ThenByDescending(RazredMarze)
                .ThenBy(_ => Guid.NewGuid())
                .ToList();

            var minimum = _o.MinimalanBrojArtikala.GetValueOrDefault(
                korpica.Velicina ?? VelicinaKorpice.Mala, 5);
            var cilj = korpica.Price * _o.KoeficijentVrijednosti;

            // Koliko sadržaj smije koštati u nabavci da korpica zadrži minimalnu maržu.
            // Važi samo za artikle sa unesenom nabavnom cijenom; ostali se javljaju adminu.
            var nabavniBudzet = korpica.Price * (1 - _o.MinimalnaMarza);

            var sastav = Sastavi(bazen, korpica.Velicina, minimum, cilj, nabavniBudzet, korpica.TezinaOd, korpica.TezinaDo);

            var sadrzaj = new SedmicniSadrzaj
            {
                KorpicaId = korpica.Id,
                Godina = c.Godina,
                Sedmica = c.Sedmica,
                Status = StatusSadrzaja.Nacrt,
            };
            foreach (var s in sastav.Stavke)
                sadrzaj.Stavke.Add(new StavkaSadrzaja
                {
                    ProductId = s.Artikal.Id,
                    Kolicina = s.Kolicina,
                    JedinicnaCijena = s.Artikal.Price,
                });

            DodajZamjene(sadrzaj, bazen, cilj, nabavniBudzet);
            db.SedmicniSadrzaji.Add(sadrzaj);

            var vrijednost = sastav.Vrijednost;
            var nabavno = sastav.Nabavno;
            var bezNabavne = sastav.Stavke.Where(s => s.Nabavna is null).Select(s => s.Artikal.Name).ToList();

            var upozorenja = new List<string>();
            var glavneVan = bazen.Where(p => p.GlavnoU(mjesec) && sadrzaj.Stavke.All(s => s.ProductId != p.Id))
                .Select(p => p.Name).ToList();
            if (glavneVan.Count > 0)
                upozorenja.Add(korpica.TezinaDo is > 0
                    ? $"Glavno u sezoni nije stalo u korpicu (budžet, zaliha ili težina): {string.Join(", ", glavneVan)}."
                    : $"Glavno u sezoni nije stalo u korpicu (budžet ili zaliha): {string.Join(", ", glavneVan)}.");
            // Kriv je ili mali bazen (zima, malo zalihe) ili budžet marže — admin mora znati koji, jer su
            // rješenja različita.
            var podobnih = bazen.Count(p => !p.Opciono && p.Price > 0);
            if (sadrzaj.Stavke.Count < minimum && podobnih >= minimum && sastav.TezinaOgranicila)
                upozorenja.Add(
                    $"U {korpica.TezinaDo:0.##} kg staje samo {sadrzaj.Stavke.Count} od {minimum} kultura. Smanji zadate " +
                    "količine za ovu veličinu, podigni najveću težinu ili spusti minimum artikala u postavkama.");
            else if (sadrzaj.Stavke.Count < minimum && podobnih >= minimum && sastav.NabavnoOgranicilo)
                upozorenja.Add(
                    $"Nabavni budžet ({nabavniBudzet:0.##} KM uz maržu {_o.MinimalnaMarza * 100:0.#} %) je dovoljan za samo " +
                    $"{sadrzaj.Stavke.Count} od {minimum} artikala. Podigni cijenu korpice, smanji zadate količine za ovu " +
                    "veličinu ili spusti maržu ili minimum artikala u postavkama.");
            else if (sadrzaj.Stavke.Count < minimum && podobnih >= minimum)
                upozorenja.Add(
                    $"Vrijednost korpice ({cilj:0.##} KM) je dovoljna za samo {sadrzaj.Stavke.Count} od {minimum} artikala. " +
                    "Smanji zadate količine za ovu veličinu ili podigni cijenu korpice.");
            else if (sadrzaj.Stavke.Count < minimum)
                upozorenja.Add($"U bazenu je premalo artikala: {sadrzaj.Stavke.Count} od {minimum}. Dopuni sadržaj ručno.");
            else if (sastav.NabavnoOgranicilo && vrijednost < cilj * 0.95m)
                upozorenja.Add(
                    $"Nabavni budžet ({nabavniBudzet:0.##} KM za maržu {_o.MinimalnaMarza * 100:0.#}%) ograničio je sadržaj " +
                    $"na {vrijednost:0.##} KM od cilja {cilj:0.##} KM. Dodaj artikle sa boljom maržom ili podigni cijenu korpice.");

            // Puna težina, a vrijednost daleko ispod cilja: kupac plaća više nego što dobija. To nije greška
            // generatora nego neslaganje cijene korpice i raspona težine — vlasnik mora odlučiti šta mijenja.
            if (korpica.TezinaDo is > 0 && sastav.Tezina >= korpica.TezinaDo.Value - 0.1m && vrijednost < cilj * 0.95m)
                upozorenja.Add(
                    $"Najveća težina ({korpica.TezinaDo:0.##} kg) je popunjena, a sadržaj vrijedi samo {vrijednost:0.##} KM " +
                    $"od cilja {cilj:0.##} KM (korpica košta {korpica.Price:0.##} KM). Podigni najveću težinu, spusti cijenu " +
                    "korpice ili dodaj skuplje kulture po kilogramu.");

            if (korpica.TezinaOd is > 0 && sastav.Tezina < korpica.TezinaOd.Value - Tolerancija)
                upozorenja.Add(
                    $"Sadržaj teži {sastav.Tezina:0.##} kg, a korpica treba najmanje {korpica.TezinaOd:0.##} kg — " +
                    "vrijednost, budžet ili zaliha ne puštaju više. Dodaj ručno nešto jeftinije po kilogramu.");
            var bezTezine = sastav.Stavke.Where(s => s.KgPoJedinici is null).Select(s => s.Artikal.Name).ToList();
            if ((korpica.TezinaOd is > 0 || korpica.TezinaDo is > 0) && bezTezine.Count > 0)
                upozorenja.Add(
                    $"Težina komada nije unesena (ne računa se u težinu korpice): {string.Join(", ", bezTezine)}. " +
                    "Unesi je u Postavkama korpica → Artikli.");

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

    /* ---------------- sastavljanje ---------------- */

    /// <summary>
    /// Najveći dio najveće težine korpice koji smije zauzeti jedna sezonska kultura (prioritet veći od 0).
    /// Bez ove granice je mala korpica od 4 kg nosila 2 kg cvekle.
    /// </summary>
    internal const decimal UdioSezonskeKulture = 0.25m;

    /// <summary>Isto za osnovnu namirnicu (prioritet 0): krompir i luk smiju više, ali ne pola korpice preko.</summary>
    internal const decimal UdioOsnovneKulture = 0.5m;

    /// <summary>Koliko sadržaj smije preći gornju granicu težine korpice (0,10 = 10 %).</summary>
    internal const decimal DozvoljenoPrekoTezine = 0.10m;

    internal sealed class Izabrani(Product artikal, decimal najmanje, decimal standardno, bool zadato, decimal najvise)
    {
        public Product Artikal { get; } = artikal;
        public decimal Najmanje { get; } = najmanje;
        public decimal Standardno { get; } = standardno;
        public bool Zadato { get; } = zadato;
        /// <summary>Najviše koliko smije ući: zaliha i udio u težini korpice.</summary>
        public decimal Najvise { get; } = najvise;
        public decimal Kolicina { get; set; } = najmanje;
        public decimal? Nabavna => Artikal.NabavnaCijena is > 0 ? Artikal.NabavnaCijena.Value : null;
        /// <summary>Kg po jedinici; null = komad bez unesene težine (ne računa se u težinu).</summary>
        public decimal? KgPoJedinici => Artikal.KgPoJedinici;
        public decimal Vrijednost => Kolicina * Artikal.Price;
        public decimal NabavnaVrijednost => Kolicina * (Nabavna ?? 0m);
        public decimal Tezina => Kolicina * (KgPoJedinici ?? 0m);
    }

    internal sealed record Sastav(List<Izabrani> Stavke, bool NabavnoOgranicilo, bool TezinaOgranicila = false)
    {
        public decimal Vrijednost => Stavke.Sum(s => s.Vrijednost);
        public decimal Nabavno => Stavke.Sum(s => s.NabavnaVrijednost);
        public decimal Tezina => Stavke.Sum(s => s.Tezina);
    }

    /// <summary>Sastavlja kutiju iz bazena koji je već poredan po važnosti. Bez baze.</summary>
    internal static Sastav Sastavi(
        IReadOnlyList<Product> bazen, VelicinaKorpice? velicina, int minimum, decimal cilj, decimal nabavniBudzet,
        decimal? tezinaOd = null, decimal? tezinaDo = null)
    {
        var poKulturi = cilj / Math.Max(minimum, 1);
        // Bez gornje granice težina ne ograničava ništa — mliječna i suhomesnata korpica. Granica je
        // okvirna: smije se preći za DozvoljenoPrekoTezine (mala 5 kg → do 5,5 kg), ali ne više.
        var najvecaTezina = tezinaDo is > 0 ? tezinaDo.Value * (1 + DozvoljenoPrekoTezine) : decimal.MaxValue;
        var kandidati = bazen
            .Where(p => p.Price > 0 && !p.Opciono)
            .Select(p => Kandidat(p, velicina, poKulturi, tezinaDo))
            .OfType<Izabrani>()
            .ToList();

        var izabrani = new List<Izabrani>();
        var nabavnoOgranicilo = false;
        var tezinaOgranicila = false;
        decimal V() => izabrani.Sum(s => s.Vrijednost);
        decimal N() => izabrani.Sum(s => s.NabavnaVrijednost);
        decimal G() => izabrani.Sum(s => s.Tezina);

        // Prvi prolaz preskače drugu vrstu iste namirnice; drugi je dozvoljava.
        IEnumerable<Izabrani> Redom() =>
            new[] { false, true }.SelectMany(istaVrsta => kandidati.Where(k =>
                !izabrani.Contains(k) && (istaVrsta || !izabrani.Any(i => IstaVrsta(i.Artikal, k.Artikal)))));

        // Koliko kultura uopšte može stati (najjeftinije, sa najmanjim količinama). Kad je to manje od
        // minimuma, rezerva se drži za taj broj — inače ne bi ušao nijedan artikal.
        var trazeno = Math.Min(minimum, kandidati.Count);
        var dostizno = Dostizno(kandidati, trazeno, cilj, nabavniBudzet, najvecaTezina);
        if (dostizno < trazeno)
        {
            var bezBudzeta = Dostizno(kandidati, trazeno, cilj, decimal.MaxValue, najvecaTezina);
            var bezTezine = Dostizno(kandidati, trazeno, cilj, nabavniBudzet, decimal.MaxValue);
            if (bezBudzeta > dostizno) nabavnoOgranicilo = true;
            if (bezTezine > dostizno) tezinaOgranicila = true;
        }

        // 1) Izbor kultura sa najmanjim količinama, uz rezervu za one koje tek trebaju doći.
        foreach (var k in Redom())
        {
            if (izabrani.Count >= minimum) break;
            if (izabrani.Contains(k)) continue;

            var v = V() + k.Vrijednost;
            var n = N() + k.NabavnaVrijednost;
            var g = G() + k.Tezina;
            if (v > cilj + Tolerancija) continue;
            if (n > nabavniBudzet + Tolerancija) { nabavnoOgranicilo = true; continue; }
            if (g > najvecaTezina + Tolerancija) { tezinaOgranicila = true; continue; }

            // Rezerva: najjeftinije (i najlakše) preostale kulture moraju i dalje stati.
            var treba = dostizno - izabrani.Count - 1;
            var ostali = kandidati.Where(o => o != k && !izabrani.Contains(o)).ToList();
            if (treba > 0)
            {
                var rezervaV = ostali.Select(o => o.Vrijednost).OrderBy(x => x).Take(treba).Sum();
                var rezervaN = ostali.Select(o => o.NabavnaVrijednost).OrderBy(x => x).Take(treba).Sum();
                var rezervaG = ostali.Select(o => o.Tezina).OrderBy(x => x).Take(treba).Sum();
                if (v + rezervaV > cilj + Tolerancija) continue;
                if (n + rezervaN > nabavniBudzet + Tolerancija) { nabavnoOgranicilo = true; continue; }
                if (g + rezervaG > najvecaTezina + Tolerancija) { tezinaOgranicila = true; continue; }
            }

            izabrani.Add(k);
        }

        // 2) Količine do zadate (standardne), redom po važnosti.
        foreach (var s in izabrani)
            nabavnoOgranicilo |= Povecaj(s, s.Standardno, izabrani, cilj, nabavniBudzet, najvecaTezina);

        // 3) Još ispod 95 % cilja: dodatna kultura ili dvije, pa veće količine artikala bez zadate.
        var dodatnih = 0;
        foreach (var k in Redom())
        {
            if (V() >= cilj * 0.95m || dodatnih >= NajviseDodatnih) break;
            if (izabrani.Contains(k)) continue;
            // Kultura preko minimuma ulazi samo cijela (zadata/standardna količina), ne kao ostatak.
            if (V() + k.Standardno * k.Artikal.Price > cilj + Tolerancija) continue;
            if (N() + k.Standardno * (k.Nabavna ?? 0m) > nabavniBudzet + Tolerancija) { nabavnoOgranicilo = true; continue; }
            if (G() + k.Standardno * (k.KgPoJedinici ?? 0m) > najvecaTezina + Tolerancija) continue;

            k.Kolicina = k.Standardno;
            izabrani.Add(k);
            dodatnih++;
        }

        foreach (var s in izabrani.Where(s => !s.Zadato))
        {
            if (V() >= cilj * 0.95m) break;
            nabavnoOgranicilo |= Povecaj(s, s.Najvise, izabrani, cilj, nabavniBudzet, najvecaTezina);
        }

        // 4) Ispod najmanje težine: dopuna redom po važnosti (osnova prva — krompir i luk su
        // najjeftiniji po kilogramu), do udjela kulture u težini, u granicama vrijednosti i budžeta.
        if (tezinaOd is > 0)
            foreach (var s in izabrani.Where(s => s.KgPoJedinici is > 0))
            {
                if (G() >= tezinaOd.Value - Tolerancija) break;
                var nedostaje = tezinaOd.Value - G();
                var doKolicine = Math.Min(s.Najvise, s.Kolicina + ZaokruziNavise(nedostaje / s.KgPoJedinici!.Value, s.Artikal.Unit));
                nabavnoOgranicilo |= Povecaj(s, doKolicine, izabrani, cilj, nabavniBudzet, najvecaTezina);
            }

        return new Sastav(izabrani, nabavnoOgranicilo, tezinaOgranicila);
    }

    /// <summary>Najviše koliko kultura (do <paramref name="najvise"/>) staje sa najmanjim količinama najjeftinijih i najlakših.</summary>
    private static int Dostizno(List<Izabrani> kandidati, int najvise, decimal cilj, decimal nabavniBudzet, decimal najvecaTezina)
    {
        var v = kandidati.Select(k => k.Vrijednost).OrderBy(x => x).ToList();
        var n = kandidati.Select(k => k.NabavnaVrijednost).OrderBy(x => x).ToList();
        var g = kandidati.Select(k => k.Tezina).OrderBy(x => x).ToList();
        decimal sv = 0, sn = 0, sg = 0;
        var broj = 0;
        while (broj < najvise)
        {
            sv += v[broj];
            sn += n[broj];
            sg += g[broj];
            if (sv > cilj + Tolerancija || sn > nabavniBudzet + Tolerancija || sg > najvecaTezina + Tolerancija) break;
            broj++;
        }
        return broj;
    }

    /// <summary>
    /// Najmanja i standardna količina artikla za ovu veličinu. Standardna je zadata količina, a bez nje
    /// dio cilja po kulturi; najmanja je pola standardne (isto pravilo kao u adminovom editoru,
    /// <c>najmanjaKolicina</c>). Null kad ni najmanja ne staje u zalihu.
    ///
    /// Uz gornju granicu težine korpice kultura ne smije preći svoj udio u njoj (<see cref="UdioSezonskeKulture"/>,
    /// <see cref="UdioOsnovneKulture"/>): zadata i najmanja količina se spuštaju do tog udjela, a komad koji
    /// je sam teži od udjela (velika bundeva u maloj korpici) ne ulazi. Komad bez unesene težine se ne mjeri.
    /// </summary>
    private static Izabrani? Kandidat(Product p, VelicinaKorpice? velicina, decimal poKulturi, decimal? tezinaDo = null)
    {
        var korak = KoracZaokruzivanja(p.Unit);
        var zadato = p.KolicinaUKorpici(velicina);
        var standardno = zadato is decimal z ? z : Math.Max(korak, Zaokruzi(poKulturi / p.Price, p.Unit));
        // Najmanja smislena količina (Product.NajmanjaKolicinaUKorpici) — nikad pola zadate: to je
        // davalo 0,1 kg bijelog luka. Osnova (prioritet 0) ulazi samo u punoj zadatoj količini.
        var najmanje = Math.Max(korak, p.NajmanjaKolicinaUKorpici());
        // Osnova ide u punoj zadatoj — osim kad je admin izričito dozvolio manje (NajmanjeUKorpici).
        if (p.PrioritetUKorpici <= 0 && p.NajmanjeUKorpici is not > 0 && zadato is decimal punaZadata)
            najmanje = Math.Max(najmanje, punaZadata);
        standardno = Math.Max(standardno, najmanje);

        var najvise = Zaokruzi(p.Stock, p.Unit);
        if (tezinaDo is > 0 && p.KgPoJedinici is decimal kg)
        {
            var udio = p.PrioritetUKorpici <= 0 ? UdioOsnovneKulture : UdioSezonskeKulture;
            najvise = Math.Min(najvise, Zaokruzi(tezinaDo.Value * (1 + DozvoljenoPrekoTezine) * udio / kg, p.Unit));
        }

        // Ne staje ni u najmanjoj količini (zaliha ili udio u težini) → kultura se izostavlja, ne smanjuje.
        if (najvise < najmanje) return null;
        return new Izabrani(p, najmanje, Math.Min(standardno, najvise), zadato is not null, najvise);
    }

    /// <summary>
    /// Diže količinu prema <paramref name="doKolicine"/> koliko puštaju cilj vrijednosti, nabavni budžet
    /// i najveća težina korpice. True kad je budžet zaustavio povećanje.
    /// </summary>
    private static bool Povecaj(
        Izabrani s, decimal doKolicine, List<Izabrani> izabrani, decimal cilj, decimal nabavniBudzet,
        decimal najvecaTezina = decimal.MaxValue)
    {
        var zelja = Math.Max(0m, Math.Min(doKolicine, s.Najvise) - s.Kolicina);
        if (zelja <= 0) return false;

        var slobodnoV = Math.Max(0m, cilj - izabrani.Sum(i => i.Vrijednost) + Tolerancija);
        var dodatak = Math.Min(zelja, Zaokruzi(slobodnoV / s.Artikal.Price, s.Artikal.Unit));
        if (s.KgPoJedinici is > 0 && najvecaTezina < decimal.MaxValue)
        {
            var slobodnoG = Math.Max(0m, najvecaTezina - izabrani.Sum(i => i.Tezina) + Tolerancija);
            dodatak = Math.Min(dodatak, Zaokruzi(slobodnoG / s.KgPoJedinici.Value, s.Artikal.Unit));
        }
        var budzetStao = false;
        if (s.Nabavna is decimal nab)
        {
            var slobodnoN = Math.Max(0m, nabavniBudzet - izabrani.Sum(i => i.NabavnaVrijednost) + Tolerancija);
            var poN = Zaokruzi(slobodnoN / nab, s.Artikal.Unit);
            if (poN < dodatak) { dodatak = poN; budzetStao = true; }
        }
        dodatak = Zaokruzi(dodatak, s.Artikal.Unit);
        if (dodatak > 0) s.Kolicina += dodatak;
        return budzetStao;
    }

    /// <summary>
    /// Dvije vrste iste namirnice (crveni i domaći krompir, crni i mladi luk, jabuka idared i kožara)?
    /// Poredi osnove riječi naziva; pridjev ispred imenice (mladi, crvena, žuta, sušene…) se ne broji.
    /// </summary>
    internal static bool IstaVrsta(Product a, Product b)
    {
        var x = Osnove(a.Name);
        return Osnove(b.Name).Any(x.Contains);
    }

    private static HashSet<string> Osnove(string naziv)
    {
        var rijeci = naziv.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var osnove = new HashSet<string>();
        for (var i = 0; i < rijeci.Count; i++)
        {
            var r = rijeci[i];
            if (r.Length < 3 || !r.All(char.IsLetter)) continue;
            if (i < rijeci.Count - 1 && Pridjev(r)) continue;
            osnove.Add(r.Length > 4 ? r[..4] : r);
        }
        return osnove;
    }

    /// <summary>
    /// Riječ ispred imenice koja je pridjev: muški i množina na -i/-e (mladi, crveni, sušene), ženski na -a
    /// (crvena, žuta, ljetna). Imenice na -a koje stoje ispred sorte (jabuka idared, paprika roga,
    /// salata kristalka, bundeva hokaido, babura paprika) završavaju drugačije pa se broje.
    /// </summary>
    private static bool Pridjev(string r) =>
        r.EndsWith('i') || r.EndsWith('e')
        || (r.EndsWith('a') && !new[] { "ika", "uka", "ška", "ata", "iva", "eva", "ura", "ija", "nja" }.Any(r.EndsWith));

    /* ---------------- podaci iz baze ---------------- */

    /// <summary>
    /// Potražnja po artiklu zadnjih <see cref="SedmicaPotraznje"/> sedmica: koliko puta je kupljen sam
    /// (Špajz) ili ga je kupac sam dodao u korpicu, minus kupci koji ga izbjegavaju (dvostruko).
    /// </summary>
    private async Task<Dictionary<Guid, int>> PotraznjaAsync(int godina, int sedmica, CancellationToken ct)
    {
        var od = DateTime.SpecifyKind(
            ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Monday).AddDays(-7 * SedmicaPotraznje), DateTimeKind.Utc);

        var kupljeno = await db.OrderItems
            .Where(i => i.Order!.CreatedAt >= od && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId)
            .Select(g => new { g.Key, Broj = g.Count() })
            .ToListAsync(ct);
        var dodato = await db.StavkeKorpiceNarudzbe
            .Where(s => s.DodaoKupac && s.OrderItem!.Order!.CreatedAt >= od && s.OrderItem.Order.Status != OrderStatus.Cancelled)
            .GroupBy(s => s.ProductId)
            .Select(g => new { g.Key, Broj = g.Count() })
            .ToListAsync(ct);
        var izbjegnuto = await db.IzbjegnutiArtikli
            .GroupBy(i => i.ProductId)
            .Select(g => new { g.Key, Broj = g.Count() })
            .ToListAsync(ct);

        var rez = new Dictionary<Guid, int>();
        foreach (var x in kupljeno) rez[x.Key] = rez.GetValueOrDefault(x.Key) + x.Broj;
        foreach (var x in dodato) rez[x.Key] = rez.GetValueOrDefault(x.Key) + x.Broj;
        foreach (var x in izbjegnuto) rez[x.Key] = rez.GetValueOrDefault(x.Key) - 2 * x.Broj;
        return rez;
    }

    /// <summary>Artikli koji su bili u sadržaju svake korpice prethodne sedmice.</summary>
    private async Task<Dictionary<Guid, HashSet<Guid>>> ProslaSedmicaAsync(int godina, int sedmica, CancellationToken ct)
    {
        var prije = ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).AddDays(-7);
        var (g, s) = (ISOWeek.GetYear(prije), ISOWeek.GetWeekOfYear(prije));
        var stavke = await db.StavkeSadrzaja
            .Where(st => st.SedmicniSadrzaj!.Godina == g && st.SedmicniSadrzaj.Sedmica == s)
            .Select(st => new { st.SedmicniSadrzaj!.KorpicaId, st.ProductId })
            .ToListAsync(ct);
        return stavke.GroupBy(x => x.KorpicaId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ProductId).ToHashSet());
    }

    /// <summary>
    /// Bira predložene zamjene: artikle iz bazena koji nisu u kutiji. Kupac jedan artikal
    /// mijenja za drugi iste vrijednosti, pa zamjena ne smije imati lošiji odnos nabavne i
    /// prodajne cijene od onog koji drži cijelu kutiju u marži (nabavni budžet / cilj) —
    /// inače bi svaka zamjena kutiju gurala ispod marže. Zamjena bez nabavne cijene se
    /// dozvoljava (ne zna se), ali ide na kraj.
    ///
    /// Biraju se SAMO artikli iz kategorija korpice (bazen): u korpicu povrća ne ide pavlaka,
    /// sir ni med, ni kao predložena zamjena (odluka vlasnika, 13.11.2026).
    /// </summary>
    private void DodajZamjene(SedmicniSadrzaj sadrzaj, List<Product> bazen, decimal cilj, decimal nabavniBudzet)
    {
        if (_o.BrojZamjena <= 0 || cilj <= 0) return;

        var najveciOdnos = nabavniBudzet / cilj;
        var uKutiji = sadrzaj.Stavke.Select(s => s.ProductId).ToHashSet();

        // Opcione kulture su prve među zamjenama — to je jedini način da stignu do kupca.
        var izbor = bazen
            .Where(p => !uKutiji.Contains(p.Id) && p.Price > 0)
            .Where(p => p.NabavnaCijena is not > 0 || p.NabavnaCijena.Value / p.Price <= najveciOdnos)
            .OrderByDescending(p => p.Opciono)
            .ThenByDescending(RazredMarze)
            .ThenBy(_ => Guid.NewGuid())
            .Take(_o.BrojZamjena);

        foreach (var p in izbor)
            sadrzaj.Zamjene.Add(new ZamjenaSadrzaja { ProductId = p.Id, JedinicnaCijena = p.Price });
    }

    /// <summary>Potražnja u grubim razredima (0, 1, 2–3, 4–7…) da unutar razreda ostane nasumičnosti; −1 = izbjegavaju ga.</summary>
    private static int RazredPotraznje(int broj) =>
        broj < 0 ? -1 : (int)Math.Floor(Math.Log2(1 + broj));

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

    private static decimal ZaokruziNavise(decimal kolicina, EPiljara.Domain.Enums.Unit jedinica)
    {
        var korak = KoracZaokruzivanja(jedinica);
        return Math.Ceiling(kolicina / korak) * korak;
    }

    /// <summary>Mjesec u koji pada četvrtak zadate ISO sedmice — četvrtak je uvijek u „pravom" mjesecu.</summary>
    private static int MjesecSedmice(int godina, int sedmica) =>
        ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Thursday).Month;
}
