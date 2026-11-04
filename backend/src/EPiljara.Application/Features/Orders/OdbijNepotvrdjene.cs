using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Poeni;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Orders;

/// <summary>
/// Odbija (Cancelled) narudžbe koje su na čekanju kad prođe rok naručivanja njihove sedmice.
/// Nabavka ide samo po potvrđenim narudžbama, pa nepotvrđena poslije roka ne može biti
/// isporučena — bolje da kupac to vidi odmah nego da čeka korpicu koja ne stiže.
///
/// Rok je isti kao za zaključavanje sadržaja (<see cref="ZakljucajSadrzajeHandler"/>).
/// Artikli iz Špajza se vraćaju na stanje (checkout ih je skinuo); sadržaj korpica se
/// oslobađa sam, jer obećano ne broji otkazane narudžbe. Potrošeni poeni se vraćaju.
/// <paramref name="Sada"/> postoji radi testova; u radu ostaje null.
/// </summary>
public record OdbijNepotvrdjeneCommand(DateTime? Sada = null) : IRequest<int>;

public class OdbijNepotvrdjeneHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije, IOptions<PoeniOpcije> poeni)
    : IRequestHandler<OdbijNepotvrdjeneCommand, int>
{
    public async Task<int> Handle(OdbijNepotvrdjeneCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;
        var (dan, vrijemeOd, cutoffSati) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);

        var naCekanju = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync(ct);
        var istekle = naCekanju
            .Where(o => sada > SedmicaHelper.DatumIsporuke(o.Godina, o.Sedmica, dan, vrijemeOd).AddHours(-cutoffSati))
            .ToList();
        if (istekle.Count == 0) return 0;

        var artikli = istekle.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        var proizvodi = await db.Products.IgnoreQueryFilters()
            .Where(p => artikli.Contains(p.Id) && p.Vrsta != VrstaProizvoda.Korpica)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var o in istekle)
        {
            await PoeniPravila.ZabiljeziPromjenuStatusaAsync(db, o, OrderStatus.Cancelled, poeni.Value, ct);
            o.TransitionTo(OrderStatus.Cancelled);
            foreach (var i in o.Items)
                if (proizvodi.TryGetValue(i.ProductId, out var p)) p.Stock += i.Quantity;
        }

        await db.SaveChangesAsync(ct);
        return istekle.Count;
    }
}
