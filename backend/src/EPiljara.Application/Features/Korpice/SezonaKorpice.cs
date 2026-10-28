using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// „Šta dobijaš u korpici?" (po uzoru na farmit): za svaki mjesec kulture koje se tada mogu
/// naći u korpici te vrste, sa okvirnom količinom po veličini.
/// </summary>
public record SezonaKorpiceDto
{
    public IReadOnlyList<VelicinaSezoneDto> Velicine { get; init; } = [];
    public IReadOnlyList<MjesecSezoneDto> Mjeseci { get; init; } = [];
}

/// <summary>Veća korpica nosi više namirnica, a ne veće porcije — zato se to kaže uz veličinu.</summary>
public record VelicinaSezoneDto
{
    public string Velicina { get; init; } = string.Empty;
    /// <summary>Najmanje koliko različitih namirnica generator stavi u kutiju.</summary>
    public int BrojNamirnica { get; init; }
    /// <summary>Vrijednost sadržaja po cijenama artikala (cijena korpice × koeficijent).</summary>
    public decimal Vrijednost { get; init; }
}

public record MjesecSezoneDto
{
    /// <summary>1–12.</summary>
    public int Mjesec { get; init; }
    public IReadOnlyList<KulturaSezoneDto> Kulture { get; init; } = [];
}

public record KulturaSezoneDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public string? ImageUrl { get; init; }
    public string Boja { get; init; } = string.Empty;
    /// <summary>Okvirna količina po veličini korpice (mala | srednja | velika).</summary>
    public IReadOnlyDictionary<string, decimal> Kolicine { get; init; } = new Dictionary<string, decimal>();
}

public record GetSezonaKorpiceQuery(string? Tip) : IRequest<SezonaKorpiceDto>;

public class GetSezonaKorpiceHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSezonaKorpiceQuery, SezonaKorpiceDto>
{
    public async Task<SezonaKorpiceDto> Handle(GetSezonaKorpiceQuery q, CancellationToken ct)
    {
        var tip = SedmicaHelper.IzSluga(q.Tip)
            ?? throw new NotFoundException("Nepoznata vrsta korpice.");
        var o = opcije.Value;

        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.TipKorpice == tip && p.IsAvailable && p.Velicina != null)
            .ToListAsync(ct);
        var kategorije = await db.KorpicaKategorije
            .Where(k => k.TipKorpice == tip)
            .Select(k => k.CategoryId)
            .ToListAsync(ct);

        // Zaliha se ne gleda: ovo je pregled cijele godine, ne ponuda ove sedmice.
        var kulture = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Obican && p.MozeUKorpicu && p.IsAvailable && p.Price > 0
                        && kategorije.Contains(p.CategoryId))
            .OrderBy(p => p.PrioritetUKorpici).ThenBy(p => p.Name)
            .ToListAsync(ct);

        // Okvirna količina = ono što generator stavi u kutiju dok puni minimum artikala:
        // cilj korpice podijeljen na minimalan broj artikala, zaokružen na korak jedinice.
        var poStavci = korpice.ToDictionary(
            k => k.Velicina!.Value.ToString().ToLowerInvariant(),
            k => k.Price * o.KoeficijentVrijednosti / Math.Max(o.MinimalanBrojArtikala.GetValueOrDefault(k.Velicina!.Value, 5), 1));

        return new SezonaKorpiceDto
        {
            Velicine = korpice.OrderBy(k => k.Velicina).Select(k => new VelicinaSezoneDto
            {
                Velicina = k.Velicina!.Value.ToString().ToLowerInvariant(),
                BrojNamirnica = o.MinimalanBrojArtikala.GetValueOrDefault(k.Velicina!.Value, 5),
                Vrijednost = Math.Round(k.Price * o.KoeficijentVrijednosti, 2),
            }).ToList(),
            Mjeseci = Enumerable.Range(1, 12).Select(m => new MjesecSezoneDto
            {
                Mjesec = m,
                Kulture = KorpicaPodobnost.USezoni(kulture, m).Select(p => new KulturaSezoneDto
                {
                    ProductId = p.Id,
                    Naziv = p.Name,
                    Jedinica = p.Unit.ToApi(),
                    ImageUrl = p.ImageUrl,
                    Boja = BojaProizvoda.Za(p.Boja, p.Name),
                    Kolicine = poStavci.ToDictionary(v => v.Key, v => Math.Max(
                        GenerisiSedmicniSadrzajHandler.Zaokruzi(v.Value / p.Price, p.Unit),
                        GenerisiSedmicniSadrzajHandler.KoracZaokruzivanja(p.Unit))),
                }).ToList(),
            }).ToList(),
        };
    }
}
