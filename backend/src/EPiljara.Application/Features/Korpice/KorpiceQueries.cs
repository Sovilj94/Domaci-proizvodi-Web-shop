using EPiljara.Application.Common;
using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Application.Features.Pretplate;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/* ---------- pomoćno ---------- */

public static class SedmicaHelper
{
    /// <summary>
    /// Vremenska zona poslovanja (A4). `DeliverySlot.TimeFrom` („16:00") je lokalno vrijeme u Banjoj
    /// Luci; do A4 se čitao kao UTC, pa se rok zatvarao dva sata (ljeti) kasnije od nominalnog.
    /// IANA ime radi na Linuxu i na Windowsu (.NET sa ICU); rezervno Windows ime.
    /// </summary>
    public static readonly TimeZoneInfo Zona = NadjiZonu();

    private static TimeZoneInfo NadjiZonu()
    {
        foreach (var id in new[] { "Europe/Sarajevo", "Central European Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>ISO sedmica po lokalnom kalendaru (ponedjeljak počinje u ponoć po Sarajevu, ne po UTC-u).</summary>
    public static (int Godina, int Sedmica) Tekuca(DateTime? sada = null)
    {
        var utc = sada ?? DateTime.UtcNow;
        var d = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);
        return (ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d));
    }

    /// <summary>
    /// Datum i VRIJEME isporuke za zadatu ISO sedmicu, za dan i vrijeme koje pozivalac
    /// prosljeđuje. Ciklus je vezan za <see cref="DeliverySlot"/>, ne za zakucan dan/sat —
    /// pozivalac ih dobija preko <see cref="UcitajRokAsync"/>.
    ///
    /// <see cref="ISOWeek.ToDateTime"/> vraća PONOĆ tog dana sa <see cref="DateTimeKind.Unspecified"/>.
    /// Prije ovog popravka se ponoć koristila direktno kao "vrijeme isporuke", pa je rok
    /// (ponoć - CutoffHours) ispadao do 24h ranije od stvarnog operativnog roka —
    /// DeliverySlot.TimeFrom se nikad nije čitao. Ovdje se <paramref name="vrijemeOd"/>
    /// dodaje na ponoć, a rezultat se eksplicitno obilježava kao UTC: backend ovo
    /// poredi sa DateTime.UtcNow (vidi GetTekucaSedmicaHandler, ZakljucajSadrzajeHandler),
    /// a frontend parsira ISO string sa "Z" kao UTC (vidi types/domain.ts) — bez
    /// eksplicitnog Kind-a se ta dva tumačenja razilaze za tačno UTC offset.
    /// </summary>
    public static DateTime DatumIsporuke(int godina, int sedmica, DayOfWeek danIsporuke, string vrijemeOd)
    {
        var ponoc = ISOWeek.ToDateTime(godina, sedmica, danIsporuke);
        var (sati, minuti) = ParsirajVrijeme(vrijemeOd);
        // A4: vrijeme termina je lokalno (Sarajevo); u UTC se pretvara uz ljetno/zimsko računanje.
        var lokalno = DateTime.SpecifyKind(ponoc.AddHours(sati).AddMinutes(minuti), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(lokalno, Zona);
    }

    /// <summary>
    /// TimeFrom na DeliverySlot-u je slobodan tekstualni unos ("16:00"), bez validacije
    /// na modelu. Ako je neparsabilan ili van opsega 00:00–23:59, NE rušimo zahtjev —
    /// to bi oborilo javnu stranicu /korpice zbog loše unesenog podatka u admin ekranu
    /// koji P1 još nema (vidi Important I4 u finalnom review-u). Biramo ponoć (00:00) kao
    /// neutralnu vrijednost — isto ponašanje kao prije ovog popravka, samo sada
    /// namjerno, a ne kao posljedica da se TimeFrom nikad nije čitao.
    /// </summary>
    private static (int Sati, int Minuti) ParsirajVrijeme(string vrijemeOd)
    {
        if (TimeSpan.TryParse(vrijemeOd, System.Globalization.CultureInfo.InvariantCulture, out var t)
            && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1))
            return (t.Hours, t.Minutes);

        return (0, 0);
    }

    /// <summary>
    /// Dan isporuke, vrijeme isporuke i rok naručivanja (u satima prije isporuke),
    /// izvedeni iz <see cref="DeliverySlot"/>-a sa najnižim <see cref="DeliverySlot.DayOfWeek"/> —
    /// prvog dana isporuke u sedmici. Kad u bazi ne postoji nijedan DeliverySlot
    /// (prazna baza, ili test kojem ovo nije bitno), koristi se
    /// <see cref="KorpiceOpcije"/> kao rezerva — vidi tamo.
    ///
    /// Namjerno NE gleda termin koji je kupac izabrao (odluka A5, 2026-10-19): sadržaj sedmice se
    /// zaključava i pakerska lista štampa jednom, prije prvog dana isporuke, pa i petkova narudžba
    /// mora stići do tog istog roka. Rok po izabranom terminu bi poslije zaključavanja primao
    /// narudžbe za već zatvorenu sedmicu.
    /// </summary>
    public static async Task<(DayOfWeek Dan, string VrijemeOd, int CutoffSati)> UcitajRokAsync(
        IAppDbContext db, KorpiceOpcije opcije, CancellationToken ct)
    {
        var slot = await db.DeliverySlots
            .OrderBy(s => s.DayOfWeek)
            .FirstOrDefaultAsync(ct);

        return slot is null
            ? (opcije.DanIsporukeRezerva, opcije.VrijemeIsporukeRezerva, opcije.RokSati)
            : ((DayOfWeek)slot.DayOfWeek, slot.TimeFrom, slot.CutoffHours);
    }

    /// <summary>
    /// Sedmica koju narudžba isporučuje: prva sedmica čiji rok naručivanja još nije prošao.
    ///
    /// NE koristi <see cref="Tekuca"/> direktno. Korpica je Product, a checkout ne provjerava
    /// rok — bez ovog pravila bi se poslije roka naručivala korpica za sedmicu čiji je sadržaj
    /// već zaključan i čija je pakerska lista već odštampana.
    ///
    /// Granica godine se ne računa ručno: uzima se ponedjeljak tekuće ISO sedmice, doda sedam
    /// dana, i iz tog datuma ponovo izvedu ISO godina i sedmica — pa prelaz 52/53 → 1 izlazi
    /// tačan sam od sebe.
    /// </summary>
    public static (int Godina, int Sedmica) SedmicaZaNarudzbu(
        DayOfWeek dan, string vrijemeOd, int cutoffSati, DateTime? sada = null)
    {
        var now = sada ?? DateTime.UtcNow;
        var (godina, sedmica) = Tekuca(now);

        var rok = DatumIsporuke(godina, sedmica, dan, vrijemeOd).AddHours(-cutoffSati);
        if (now <= rok) return (godina, sedmica);

        var sljedeca = ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Monday).AddDays(7);
        return (ISOWeek.GetYear(sljedeca), ISOWeek.GetWeekOfYear(sljedeca));
    }

    /// <summary>
    /// Sedmica pomjerena za <paramref name="zaSedmica"/> unaprijed (pauza pretplate).
    /// Računa se preko datuma, ne sabiranjem brojeva: 52 + 3 nije 55, a godine imaju i 52 i
    /// 53 sedmice — isti razlog zbog kojeg <see cref="SedmicaZaNarudzbu"/> ide preko
    /// ponedjeljka umjesto da ručno hvata prelaz godine.
    /// </summary>
    public static (int Godina, int Sedmica) Pomjeri(int godina, int sedmica, int zaSedmica)
    {
        var d = ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Monday).AddDays(7 * zaSedmica);
        return (ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d));
    }

    /// <summary>
    /// Je li sedmica (g1, s1) prije (g2, s2). Poređenje PARA, ne jednog broja — sedmica 1
    /// naredne godine je poslije sedmice 52 tekuće.
    /// </summary>
    public static bool SedmicaPrije(int g1, int s1, int g2, int s2) =>
        g1 != g2 ? g1 < g2 : s1 < s2;

    public static string Slug(TipKorpice t) => t switch
    {
        TipKorpice.Povrce => "povrce",
        TipKorpice.Voce => "voce",
        TipKorpice.Mlijecna => "mlijecna",
        TipKorpice.Suhomesnata => "suhomesnata",
        TipKorpice.Domacinska => "domacinska",
        _ => "povrce",
    };

    public static TipKorpice? IzSluga(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "povrce" => TipKorpice.Povrce,
        "voce" => TipKorpice.Voce,
        "mlijecna" => TipKorpice.Mlijecna,
        "suhomesnata" => TipKorpice.Suhomesnata,
        "domacinska" => TipKorpice.Domacinska,
        _ => null,
    };

    /// <summary>
    /// NE zove se ToDto: <c>MappingExtensions.ToDto(this Product)</c> već postoji i vraća
    /// ProductDto. Dvije ekstenzije istog imena nad istim tipom čine svaki poziv dvosmislenim.
    /// </summary>
    /// <param name="pretplateOpcije">
    /// Kad je prosljeđeno, DTO nosi i cijenu koju pretplatnik plaća od šeste isporuke.
    /// Računa je <see cref="PretplatnaCijena"/>, nikad klijent: popust je poslovno pravilo i
    /// kopija tog množenja u pregledaču bi se razišla sa računom čim se prag ili procenat
    /// promijene.
    /// </param>
    public static KorpicaDto ToKorpicaDto(this Product p, PretplateOpcije? pretplateOpcije = null) => new()
    {
        Id = p.Id,
        Naziv = p.Name,
        Slug = p.Slug,
        Tip = Slug(p.TipKorpice ?? TipKorpice.Povrce),
        Velicina = (p.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
        Cijena = p.Price,
        CijenaNedjeljna = pretplateOpcije is null ? null : PretplatnaCijena.Izracunaj(p.Price, PlanPretplate.Nedjeljna, pretplateOpcije),
        CijenaMjesecna = pretplateOpcije is null ? null : PretplatnaCijena.Izracunaj(p.Price, PlanPretplate.Mjesecna, pretplateOpcije),
        CijenaTriMjeseca = pretplateOpcije is null ? null : PretplatnaCijena.Izracunaj(p.Price, PlanPretplate.TriMjeseca, pretplateOpcije),
        ObavezaMjesecna = pretplateOpcije?.ObavezaMjesecna,
        ObavezaTriMjeseca = pretplateOpcije?.ObavezaTriMjeseca,
        ImageUrl = p.ImageUrl,
        Opis = p.Description,
        Stock = p.Stock,
        Dostupna = p.IsAvailable && p.Stock > 0,
    };

    public static SadrzajDto ToDto(this SedmicniSadrzaj s) => new()
    {
        Id = s.Id,
        KorpicaId = s.KorpicaId,
        Godina = s.Godina,
        Sedmica = s.Sedmica,
        Status = s.Status.ToString(),
        CiljnaVrijednost = s.CiljnaVrijednost,
        Stavke = s.Stavke.Select(st => new StavkaSadrzajaDto
        {
            ProductId = st.ProductId,
            Naziv = st.Product?.Name ?? string.Empty,
            Kolicina = st.Kolicina,
            Jedinica = (st.Product?.Unit ?? EPiljara.Domain.Enums.Unit.Kom).ToApi(),
            ImageUrl = st.Product?.ImageUrl,
            Boja = BojaProizvoda.Za(st.Product?.Boja, st.Product?.Name ?? string.Empty),
            ProizvodjacNaziv = st.Product?.Producer?.Name ?? string.Empty,
            ProizvodjacSelo = st.Product?.Producer?.Village ?? string.Empty,
        }).ToList(),
    };
}

/* ---------- lista korpica ---------- */

public record GetKorpiceQuery(string? Tip) : IRequest<IReadOnlyList<KorpicaDto>>;

public class GetKorpiceHandler(IAppDbContext db, IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<GetKorpiceQuery, IReadOnlyList<KorpicaDto>>
{
    public async Task<IReadOnlyList<KorpicaDto>> Handle(GetKorpiceQuery q, CancellationToken ct)
    {
        var tip = SedmicaHelper.IzSluga(q.Tip);

        var lista = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica)
            .Where(p => tip == null || p.TipKorpice == tip)
            .OrderBy(p => p.TipKorpice).ThenBy(p => p.Velicina)
            .ToListAsync(ct);

        return lista.Select(p => p.ToKorpicaDto(pretplateOpcije.Value)).ToList();
    }
}

/* ---------- jedna korpica ---------- */

public record GetKorpicaBySlugQuery(string Slug) : IRequest<KorpicaDto>;

public class GetKorpicaBySlugHandler(IAppDbContext db, IOptions<PretplateOpcije> pretplateOpcije)
    : IRequestHandler<GetKorpicaBySlugQuery, KorpicaDto>
{
    public async Task<KorpicaDto> Handle(GetKorpicaBySlugQuery q, CancellationToken ct)
    {
        var p = await db.Products
            .FirstOrDefaultAsync(x => x.Slug == q.Slug && x.Vrsta == VrstaProizvoda.Korpica, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");
        return p.ToKorpicaDto(pretplateOpcije.Value);
    }
}

/* ---------- sadržaj korpice za tekuću sedmicu ---------- */

public record GetSadrzajKorpiceQuery(string Slug) : IRequest<SadrzajDto>;

public class GetSadrzajKorpiceHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSadrzajKorpiceQuery, SadrzajDto>
{
    public async Task<SadrzajDto> Handle(GetSadrzajKorpiceQuery q, CancellationToken ct)
    {
        var korpica = await db.Products
            .FirstOrDefaultAsync(x => x.Slug == q.Slug && x.Vrsta == VrstaProizvoda.Korpica, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        // R2: prvo sedmica koju checkout upravo prodaje (poslije roka je to naredna), pa tekuća —
        // stranica korpice ne smije pokazivati sadržaj sedmice za koju se više ne naručuje dok
        // za narednu postoji objavljen (nalaz P4, O7).
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var prodaje = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);
        var tekuca = SedmicaHelper.Tekuca();

        var kandidati = await db.SedmicniSadrzaji
            .Include(s => s.Stavke).ThenInclude(st => st.Product).ThenInclude(p => p!.Producer)
            .Where(s => s.KorpicaId == korpica.Id && s.Status != StatusSadrzaja.Nacrt
                && ((s.Godina == prodaje.Godina && s.Sedmica == prodaje.Sedmica)
                    || (s.Godina == tekuca.Godina && s.Sedmica == tekuca.Sedmica)))
            .ToListAsync(ct);

        var sadrzaj = kandidati.FirstOrDefault(s => s.Godina == prodaje.Godina && s.Sedmica == prodaje.Sedmica)
            ?? kandidati.FirstOrDefault()
            ?? throw new NotFoundException("sadrzaj_nije_objavljen");

        return sadrzaj.ToDto();
    }
}

/* ---------- tekuća sedmica i rok ---------- */

public record GetTekucaSedmicaQuery : IRequest<SedmicaDto>;

public class GetTekucaSedmicaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetTekucaSedmicaQuery, SedmicaDto>
{
    public async Task<SedmicaDto> Handle(GetTekucaSedmicaQuery q, CancellationToken ct)
    {
        var (godina, sedmica) = SedmicaHelper.Tekuca();
        var (dan, vrijemeOd, cutoffSati) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var isporuka = SedmicaHelper.DatumIsporuke(godina, sedmica, dan, vrijemeOd);
        var rok = isporuka.AddHours(-cutoffSati);

        var (sljedecaGodina, sljedecaSedmica) = SedmicaHelper.Pomjeri(godina, sedmica, 1);
        var sljedecaIsporuka = SedmicaHelper.DatumIsporuke(sljedecaGodina, sljedecaSedmica, dan, vrijemeOd);

        return new SedmicaDto
        {
            Godina = godina,
            Sedmica = sedmica,
            DatumIsporuke = isporuka,
            RokNarucivanja = rok,
            RokIstekao = DateTime.UtcNow > rok,
            SljedecaIsporuka = sljedecaIsporuka,
            SljedeciRok = sljedecaIsporuka.AddHours(-cutoffSati),
        };
    }
}
