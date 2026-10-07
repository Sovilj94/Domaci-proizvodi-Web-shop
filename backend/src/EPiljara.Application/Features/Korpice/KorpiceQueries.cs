using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/* ---------- pomoćno ---------- */

public static class SedmicaHelper
{
    public static (int Godina, int Sedmica) Tekuca(DateTime? sada = null)
    {
        var d = sada ?? DateTime.UtcNow;
        return (ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d));
    }

    /// <summary>
    /// Datum isporuke za zadatu ISO sedmicu, za dan koji pozivalac prosljeđuje. Ciklus
    /// je vezan za <see cref="DeliverySlot"/>, ne za zakucan dan — pozivalac dan
    /// dobija preko <see cref="UcitajRokAsync"/>.
    /// </summary>
    public static DateTime DatumIsporuke(int godina, int sedmica, DayOfWeek danIsporuke) =>
        ISOWeek.ToDateTime(godina, sedmica, danIsporuke);

    /// <summary>
    /// Dan isporuke i rok naručivanja (u satima prije isporuke), izvedeni iz
    /// <see cref="DeliverySlot"/>-a sa najnižim <see cref="DeliverySlot.DayOfWeek"/> —
    /// prvog dana isporuke u sedmici. Kad u bazi ne postoji nijedan DeliverySlot
    /// (prazna baza, ili test kojem ovo nije bitno), koristi se
    /// <see cref="KorpiceOpcije"/> kao rezerva — vidi tamo.
    /// </summary>
    public static async Task<(DayOfWeek Dan, int CutoffSati)> UcitajRokAsync(
        IAppDbContext db, KorpiceOpcije opcije, CancellationToken ct)
    {
        var slot = await db.DeliverySlots
            .OrderBy(s => s.DayOfWeek)
            .FirstOrDefaultAsync(ct);

        return slot is null
            ? (opcije.DanIsporukeRezerva, opcije.RokSati)
            : ((DayOfWeek)slot.DayOfWeek, slot.CutoffHours);
    }

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
    public static KorpicaDto ToKorpicaDto(this Product p) => new()
    {
        Id = p.Id,
        Naziv = p.Name,
        Slug = p.Slug,
        Tip = Slug(p.TipKorpice ?? TipKorpice.Povrce),
        Velicina = (p.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
        Cijena = p.Price,
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
            ProizvodjacNaziv = st.Product?.Producer?.Name ?? string.Empty,
            ProizvodjacSelo = st.Product?.Producer?.Village ?? string.Empty,
        }).ToList(),
    };
}

/* ---------- lista korpica ---------- */

public record GetKorpiceQuery(string? Tip) : IRequest<IReadOnlyList<KorpicaDto>>;

public class GetKorpiceHandler(IAppDbContext db)
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

        return lista.Select(p => p.ToKorpicaDto()).ToList();
    }
}

/* ---------- jedna korpica ---------- */

public record GetKorpicaBySlugQuery(string Slug) : IRequest<KorpicaDto>;

public class GetKorpicaBySlugHandler(IAppDbContext db)
    : IRequestHandler<GetKorpicaBySlugQuery, KorpicaDto>
{
    public async Task<KorpicaDto> Handle(GetKorpicaBySlugQuery q, CancellationToken ct)
    {
        var p = await db.Products
            .FirstOrDefaultAsync(x => x.Slug == q.Slug && x.Vrsta == VrstaProizvoda.Korpica, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");
        return p.ToKorpicaDto();
    }
}

/* ---------- sadržaj korpice za tekuću sedmicu ---------- */

public record GetSadrzajKorpiceQuery(string Slug) : IRequest<SadrzajDto>;

public class GetSadrzajKorpiceHandler(IAppDbContext db)
    : IRequestHandler<GetSadrzajKorpiceQuery, SadrzajDto>
{
    public async Task<SadrzajDto> Handle(GetSadrzajKorpiceQuery q, CancellationToken ct)
    {
        var korpica = await db.Products
            .FirstOrDefaultAsync(x => x.Slug == q.Slug && x.Vrsta == VrstaProizvoda.Korpica, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        var (godina, sedmica) = SedmicaHelper.Tekuca();

        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke).ThenInclude(st => st.Product).ThenInclude(p => p!.Producer)
            .FirstOrDefaultAsync(s =>
                s.KorpicaId == korpica.Id && s.Godina == godina && s.Sedmica == sedmica
                && s.Status != StatusSadrzaja.Nacrt, ct)
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
        var (dan, cutoffSati) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var isporuka = SedmicaHelper.DatumIsporuke(godina, sedmica, dan);
        var rok = isporuka.AddHours(-cutoffSati);

        return new SedmicaDto
        {
            Godina = godina,
            Sedmica = sedmica,
            DatumIsporuke = isporuka,
            RokNarucivanja = rok,
            RokIstekao = DateTime.UtcNow > rok,
        };
    }
}
