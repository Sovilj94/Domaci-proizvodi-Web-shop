using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/*
 * Obračun cijene korpice (admin → Korpice → Obračun cijena). Za svaki mjesec se sastavi TIPIČAN sadržaj
 * korpice — glavne kulture mjeseca, pa do minimuma artikala prosjek osnovnih artikala (najmanji prioritet)
 * u sezoni, svaki u svojoj zadatoj količini za tu veličinu — i iz njega:
 *   najniža cijena  = (nabavno + kutija) / (1 − ciljna marža)        → zarada je zagarantovana
 *   najviša cijena  = vrijednost istog sadržaja u piljari × (1 − popust) → kupac prolazi povoljnije
 * Zaliha se ne gleda (ovo je planiranje cijene, ne sedmični sadržaj). Opcione kulture i artikli bez nabavne
 * cijene ne ulaze; artikal bez cijene u piljari računa se po našoj cijeni (i to se javlja).
 */

public record ObracunMjesecaDto
{
    public int Mjesec { get; init; }
    public int Artikala { get; init; }
    public int Minimum { get; init; }
    public int Glavnih { get; init; }
    public decimal Nabavno { get; init; }
    public decimal NasaVrijednost { get; init; }
    public decimal PiljaraVrijednost { get; init; }
    /// <summary>Koliko artikala tipičnog sadržaja nema cijenu u piljari (računato po našoj).</summary>
    public int BezPiljare { get; init; }
    /// <summary>Najniža cijena koja daje ciljnu maržu (sa kutijom).</summary>
    public decimal MinCijena { get; init; }
    /// <summary>Najviša cijena uz koju je korpica zadati procenat jeftinija od piljare.</summary>
    public decimal MaxCijena { get; init; }
    /// <summary>Postoji cijena koja ispunjava oba uslova.</summary>
    public bool Moguce { get; init; }
    /// <summary>Marža i ušteda kupca uz SADAŠNJU cijenu korpice.</summary>
    public decimal? MarzaSada { get; init; }
    public decimal? UstedaSada { get; init; }
}

public record ObracunKorpiceDto
{
    public Guid Id { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Tip { get; init; } = string.Empty;
    public string Velicina { get; init; } = string.Empty;
    public decimal Cijena { get; init; }
    public bool UProdaji { get; init; }
    public decimal Ambalaza { get; init; }
    public ObracunMjesecaDto Izabrani { get; init; } = new();
    public IReadOnlyList<ObracunMjesecaDto> Mjeseci { get; init; } = [];
    /// <summary>Najniža cijena za izabrani mjesec, zaokružena naviše na 0,50 KM.</summary>
    public decimal Preporuka { get; init; }
    public decimal MarzaPriPreporuci { get; init; }
    public decimal UstedaPriPreporuci { get; init; }
    /// <summary>Najniža cijena koja daje ciljnu maržu U SVAKOM mjesecu (najskuplji mjesec), na 0,50 KM.</summary>
    public decimal PreporukaGodina { get; init; }
}

public record ObracunCijenaDto
{
    public decimal Marza { get; init; }
    public decimal Povoljnije { get; init; }
    public int Mjesec { get; init; }
    public IReadOnlyList<ObracunKorpiceDto> Korpice { get; init; } = [];
    /// <summary>Artikli u korpicama bez nabavne cijene — ne ulaze u obračun.</summary>
    public int BezNabavne { get; init; }
    /// <summary>Artikli u korpicama bez cijene u piljari — računaju se po našoj cijeni.</summary>
    public int BezPiljare { get; init; }
}

/// <param name="Marza">Ciljna marža korpice, 0–0,9 (0,45 = 45 %).</param>
/// <param name="Povoljnije">Koliko korpica mora biti jeftinija od istog sadržaja u piljari, 0–0,9.</param>
/// <param name="Mjesec">1–12; null = tekući mjesec.</param>
public record GetObracunCijenaKorpicaQuery(decimal Marza, decimal Povoljnije, int? Mjesec = null) : IRequest<ObracunCijenaDto>;

public class GetObracunCijenaKorpicaValidator : AbstractValidator<GetObracunCijenaKorpicaQuery>
{
    public GetObracunCijenaKorpicaValidator()
    {
        RuleFor(x => x.Marza).InclusiveBetween(0m, 0.9m).WithMessage("Marža mora biti od 0 do 90 %.");
        RuleFor(x => x.Povoljnije).InclusiveBetween(0m, 0.9m).WithMessage("Popust u odnosu na piljaru mora biti od 0 do 90 %.");
        RuleFor(x => x.Mjesec).InclusiveBetween(1, 12).When(x => x.Mjesec is not null).WithMessage("Mjesec mora biti od 1 do 12.");
    }
}

public class GetObracunCijenaKorpicaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetObracunCijenaKorpicaQuery, ObracunCijenaDto>
{
    private sealed record Pakovanje(decimal Nabavno, decimal Nasa, decimal Piljara, bool BezPiljare);

    public async Task<ObracunCijenaDto> Handle(GetObracunCijenaKorpicaQuery q, CancellationToken ct)
    {
        var o = opcije.Value;
        var izabrani = q.Mjesec ?? ObavjestenjaMjesec();
        var korpice = await db.Products.AsNoTracking()
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.TipKorpice != null)
            .OrderBy(p => p.TipKorpice).ThenBy(p => p.Velicina)
            .ToListAsync(ct);
        var veze = await db.KorpicaKategorije.AsNoTracking().ToListAsync(ct);
        var kategorije = veze.Select(v => v.CategoryId).Distinct().ToList();
        var artikli = await db.Products.AsNoTracking()
            .Where(p => p.Vrsta == VrstaProizvoda.Obican && p.MozeUKorpicu && p.IsAvailable && p.Price > 0
                        && kategorije.Contains(p.CategoryId))
            .ToListAsync(ct);

        var rezultat = new List<ObracunKorpiceDto>();
        foreach (var k in korpice)
        {
            var kat = veze.Where(v => v.TipKorpice == k.TipKorpice).Select(v => v.CategoryId).ToHashSet();
            var bazen = artikli.Where(p => kat.Contains(p.CategoryId) && !p.Opciono && p.NabavnaCijena is > 0).ToList();
            var minimum = o.MinimalanBrojArtikala.GetValueOrDefault(k.Velicina ?? VelicinaKorpice.Mala, 5);
            var ambalaza = k.AmbalazaCijena ?? 0m;

            var mjeseci = Enumerable.Range(1, 12).Select(m => Mjesec(k, bazen, m, minimum, ambalaza, q, o)).ToList();
            var izabran = mjeseci[izabrani - 1];
            var preporuka = NaPola(izabran.MinCijena);
            var godina = NaPola(mjeseci.Where(m => m.Artikala > 0).Select(m => m.MinCijena).DefaultIfEmpty(0m).Max());
            rezultat.Add(new ObracunKorpiceDto
            {
                Id = k.Id, Naziv = k.Name, Tip = SedmicaHelper.Slug(k.TipKorpice!.Value),
                Velicina = (k.Velicina ?? VelicinaKorpice.Mala).ToString().ToLowerInvariant(),
                Cijena = k.Price, UProdaji = k.IsAvailable, Ambalaza = ambalaza,
                Izabrani = izabran, Mjeseci = mjeseci,
                Preporuka = preporuka,
                MarzaPriPreporuci = preporuka > 0 ? Math.Round((preporuka - izabran.Nabavno - ambalaza) / preporuka, 4) : 0m,
                UstedaPriPreporuci = izabran.PiljaraVrijednost > 0 ? Math.Round(1 - preporuka / izabran.PiljaraVrijednost, 4) : 0m,
                PreporukaGodina = godina,
            });
        }

        var uKorpicama = artikli.Where(p => !p.Opciono).ToList();
        return new ObracunCijenaDto
        {
            Marza = q.Marza, Povoljnije = q.Povoljnije, Mjesec = izabrani, Korpice = rezultat,
            BezNabavne = uKorpicama.Count(p => p.NabavnaCijena is not > 0),
            BezPiljare = uKorpicama.Count(p => p.CijenaPiljara is not > 0),
        };
    }

    private static ObracunMjesecaDto Mjesec(Product k, List<Product> bazen, int m, int minimum, decimal ambalaza,
        GetObracunCijenaKorpicaQuery q, KorpiceOpcije o)
    {
        var uSezoni = bazen.Where(p => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(m)).ToList();
        var glavni = uSezoni.Where(p => p.GlavnoU(m)).ToList();
        var ostali = uSezoni.Except(glavni).ToList();
        // Osnovni = najmanji prioritet među ostalima; skupe dopune ne ulaze u „tipičnu" korpicu dok ima osnovnih.
        var osnovni = ostali.Count == 0 ? [] : ostali.Where(p => p.PrioritetUKorpici == ostali.Min(x => x.PrioritetUKorpici)).ToList();
        var mjesta = Math.Max(minimum - glavni.Count, 0);
        var izvor = osnovni.Count >= mjesta ? osnovni : ostali;

        Pakovanje Pak(Product p)
        {
            // Zadata količina za veličinu; bez nje generator uzima vrijednost cilja podijeljenu na minimum.
            var kol = p.KolicinaUKorpici(k.Velicina) ?? k.Price * o.KoeficijentVrijednosti / Math.Max(minimum, 1) / p.Price;
            var piljara = p.CijenaPiljara is > 0 ? p.CijenaPiljara.Value : p.Price;
            return new Pakovanje(kol * p.NabavnaCijena!.Value, kol * p.Price, kol * piljara, p.CijenaPiljara is not > 0);
        }

        var gl = glavni.Select(Pak).ToList();
        var os = izvor.Select(Pak).ToList();
        var uzeto = Math.Min(mjesta, os.Count);
        decimal Prosjek(Func<Pakovanje, decimal> f) => os.Count == 0 ? 0m : os.Average(f) * uzeto;

        var nabavno = Math.Round(gl.Sum(x => x.Nabavno) + Prosjek(x => x.Nabavno), 2);
        var nasa = Math.Round(gl.Sum(x => x.Nasa) + Prosjek(x => x.Nasa), 2);
        var piljara = Math.Round(gl.Sum(x => x.Piljara) + Prosjek(x => x.Piljara), 2);
        var bezPiljare = gl.Count(x => x.BezPiljare)
                         + (os.Count == 0 ? 0 : (int)Math.Round((decimal)os.Count(x => x.BezPiljare) / os.Count * uzeto));
        var min = Math.Round((nabavno + ambalaza) / (1 - q.Marza), 2);
        var max = Math.Round(piljara * (1 - q.Povoljnije), 2);
        return new ObracunMjesecaDto
        {
            Mjesec = m, Artikala = gl.Count + uzeto, Minimum = minimum, Glavnih = gl.Count,
            Nabavno = nabavno, NasaVrijednost = nasa, PiljaraVrijednost = piljara, BezPiljare = bezPiljare,
            MinCijena = min, MaxCijena = max, Moguce = gl.Count + uzeto > 0 && min <= max,
            MarzaSada = k.Price > 0 && gl.Count + uzeto > 0 ? Math.Round((k.Price - nabavno - ambalaza) / k.Price, 4) : null,
            UstedaSada = piljara > 0 ? Math.Round(1 - k.Price / piljara, 4) : null,
        };
    }

    /// <summary>Naviše na 0,50 KM — cijena korpice se ne piše u parama.</summary>
    private static decimal NaPola(decimal x) => Math.Ceiling(x * 2) / 2;

    private static int ObavjestenjaMjesec() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona).Month;
}

/* ---------- postavi cijenu jedne korpice (dugme „Primijeni" u obračunu) ---------- */

public record PostaviCijenuKorpiceCommand(Guid Id, decimal Cijena) : IRequest<decimal>;

public class PostaviCijenuKorpiceValidator : AbstractValidator<PostaviCijenuKorpiceCommand>
{
    public PostaviCijenuKorpiceValidator() =>
        RuleFor(x => x.Cijena).InclusiveBetween(0.5m, 1000m).WithMessage("Cijena korpice mora biti od 0,50 do 1.000 KM.");
}

/// <summary>
/// Mijenja cijenu korpice. Narudžbe nose svoj snimak cijene, a objavljen sedmični sadržaj svoju ciljnu
/// vrijednost — nova cijena važi za nove narudžbe i sljedeće generisanje.
/// </summary>
public class PostaviCijenuKorpiceHandler(IAppDbContext db) : IRequestHandler<PostaviCijenuKorpiceCommand, decimal>
{
    public async Task<decimal> Handle(PostaviCijenuKorpiceCommand c, CancellationToken ct)
    {
        var k = await db.Products.FirstOrDefaultAsync(p => p.Id == c.Id && p.Vrsta == VrstaProizvoda.Korpica, ct)
                ?? throw new NotFoundException("Korpica nije pronađena.");
        k.Price = Math.Round(c.Cijena, 2);
        k.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return k.Price;
    }
}
