using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Koliko korpica na čekanju stanje dozvoljava da se potvrdi — po sedmici isporuke.
///
/// „Preostalo za prodaju" (Zalihe) već broji i narudžbe na čekanju, pa ne kaže smiju li se one
/// potvrditi. Ovdje se slobodno stanje računa samo naspram POTVRĐENIH korpica (sve osim
/// na čekanju i otkazanih), a korpice na čekanju se onda uzimaju redom kojim su stigle, sa
/// stvarnim sadržajem narudžbe (kupac ga je možda mijenjao). Sve korpice dijele isto stanje,
/// pa brojevi po korpicama zajedno važe.
/// </summary>
public record GetPotvrdjivanjeQuery : IRequest<IReadOnlyList<PotvrdjivanjeSedmiceDto>>;

public class GetPotvrdjivanjeHandler(IAppDbContext db)
    : IRequestHandler<GetPotvrdjivanjeQuery, IReadOnlyList<PotvrdjivanjeSedmiceDto>>
{
    public async Task<IReadOnlyList<PotvrdjivanjeSedmiceDto>> Handle(GetPotvrdjivanjeQuery q, CancellationToken ct)
    {
        var sedmice = await db.OrderItems
            .Where(i => i.Order!.Status == OrderStatus.Pending && i.KorpicaStavke.Any())
            .Select(i => new { i.Order!.Godina, i.Order.Sedmica })
            .Distinct()
            .ToListAsync(ct);
        if (sedmice.Count == 0) return [];

        var godine = sedmice.Select(s => s.Godina).Distinct().ToList();
        var redovi = (await db.StavkeKorpiceNarudzbe
                .Where(s => godine.Contains(s.OrderItem!.Order!.Godina)
                            && s.OrderItem.Order.Status != OrderStatus.Cancelled)
                .Select(s => new
                {
                    s.ProductId,
                    s.ProductName,
                    s.Kolicina,
                    s.OrderItemId,
                    s.OrderItem!.Quantity,
                    KorpicaId = s.OrderItem.ProductId,
                    Korpica = s.OrderItem.ProductName,
                    s.OrderItem.Order!.Godina,
                    s.OrderItem.Order.Sedmica,
                    s.OrderItem.Order.Status,
                    s.OrderItem.Order.CreatedAt,
                })
                .ToListAsync(ct))
            .Where(r => sedmice.Any(s => s.Godina == r.Godina && s.Sedmica == r.Sedmica))
            .ToList();

        var idjevi = redovi.Select(r => r.ProductId).Distinct().ToList();
        var stanje = await db.Products.IgnoreQueryFilters()
            .Where(p => idjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Stock, ct);

        var rezultat = new List<PotvrdjivanjeSedmiceDto>();
        foreach (var s in sedmice.OrderBy(s => s.Godina).ThenBy(s => s.Sedmica))
        {
            var uSedmici = redovi.Where(r => r.Godina == s.Godina && r.Sedmica == s.Sedmica).ToList();

            var slobodno = idjevi.ToDictionary(id => id, id => stanje.GetValueOrDefault(id));
            foreach (var r in uSedmici.Where(r => r.Status != OrderStatus.Pending))
                slobodno[r.ProductId] -= r.Kolicina * r.Quantity;

            var naCekanju = new Dictionary<Guid, (string Naziv, int Ukupno, int Moze, string? Nedostaje)>();
            var stavke = uSedmici.Where(r => r.Status == OrderStatus.Pending)
                .GroupBy(r => r.OrderItemId)
                .OrderBy(g => g.First().CreatedAt);
            foreach (var g in stavke)
            {
                var prvi = g.First();
                var kutija = (int)prvi.Quantity;
                (string Naziv, int Ukupno, int Moze, string? Nedostaje) red =
                    naCekanju.TryGetValue(prvi.KorpicaId, out var bilo) ? bilo : (prvi.Korpica, 0, 0, null);
                red.Ukupno += kutija;

                var fali = g.FirstOrDefault(r => r.Kolicina * r.Quantity > slobodno[r.ProductId]);
                if (fali is null)
                {
                    foreach (var r in g) slobodno[r.ProductId] -= r.Kolicina * r.Quantity;
                    red.Moze += kutija;
                }
                else
                {
                    red.Nedostaje ??= fali.ProductName;
                }
                naCekanju[prvi.KorpicaId] = red;
            }

            rezultat.Add(new PotvrdjivanjeSedmiceDto
            {
                Godina = s.Godina,
                Sedmica = s.Sedmica,
                Korpice = naCekanju
                    .Select(kv => new PotvrdjivanjeKorpiceDto
                    {
                        KorpicaId = kv.Key,
                        Naziv = kv.Value.Naziv,
                        NaCekanju = kv.Value.Ukupno,
                        MozeSePotvrditi = kv.Value.Moze,
                        Nedostaje = kv.Value.Nedostaje,
                    })
                    .OrderBy(k => k.Naziv)
                    .ToList(),
            });
        }
        return rezultat;
    }
}
