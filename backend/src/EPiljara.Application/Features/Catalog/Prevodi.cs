using EPiljara.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Catalog;

/// <summary>
/// Prevodi kataloga za jezik sajta: srpski tekst → engleski (nazivi proizvoda i kategorija,
/// opisi proizvoda). Frontend ih koristi isto kao rječnik interfejsa — gdje god se na stranici
/// pojavi srpski naziv, zamijeni ga engleskim. Ono bez prevoda ostaje na srpskom.
/// </summary>
public record GetPrevodiQuery : IRequest<IReadOnlyDictionary<string, string>>;

public class GetPrevodiHandler(IAppDbContext db) : IRequestHandler<GetPrevodiQuery, IReadOnlyDictionary<string, string>>
{
    public async Task<IReadOnlyDictionary<string, string>> Handle(GetPrevodiQuery q, CancellationToken ct)
    {
        var mapa = new Dictionary<string, string>();
        void Dodaj(string? sr, string? en)
        {
            if (string.IsNullOrWhiteSpace(sr) || string.IsNullOrWhiteSpace(en)) return;
            mapa.TryAdd(sr.Trim(), en.Trim());
        }

        foreach (var k in await db.Categories.Select(k => new { k.Name, k.NazivEn }).ToListAsync(ct))
            Dodaj(k.Name, k.NazivEn);
        foreach (var p in await db.Products.Select(p => new { p.Name, p.NazivEn, p.Description, p.OpisEn }).ToListAsync(ct))
        {
            Dodaj(p.Name, p.NazivEn);
            Dodaj(p.Description, p.OpisEn);
        }
        return mapa;
    }
}
