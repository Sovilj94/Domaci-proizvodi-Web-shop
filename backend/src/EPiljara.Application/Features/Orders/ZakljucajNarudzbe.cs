using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Orders;

/// <summary>ISO godina i sedmica isporuke.</summary>
public record SedmicaKljuc(int Godina, int Sedmica);

/// <summary>
/// Zaključava narudžbe kojima je prošao rok naručivanja njihove sedmice: postavlja
/// <c>ZakljucanaU</c>, status se ne mijenja. Narudžba je prihvaćena čim nastane (nema ručnog
/// odobravanja), a od zaključavanja je kupac više ne mijenja i roba ide u nabavku.
///
/// Rok je isti kao za zaključavanje sadržaja (<see cref="ZakljucajSadrzajeHandler"/>). Otkazane
/// se ne diraju; već zaključane takođe ne (prvi prolaz odlučuje, drugi ne pomjera vrijeme).
/// Usput prebacuje stare narudžbe „na čekanju" (iz vremena odobravanja) u prihvaćene.
/// Vraća sedmice u kojima je ovaj prolaz zaključao bar jednu narudžbu — za njih posao pravi
/// otkupne listove. <paramref name="Sada"/> postoji radi testova; u radu ostaje null.
/// </summary>
public record ZakljucajNarudzbeCommand(DateTime? Sada = null) : IRequest<IReadOnlyList<SedmicaKljuc>>;

public class ZakljucajNarudzbeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<ZakljucajNarudzbeCommand, IReadOnlyList<SedmicaKljuc>>
{
    public async Task<IReadOnlyList<SedmicaKljuc>> Handle(ZakljucajNarudzbeCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;
        var (dan, vrijemeOd, cutoffSati) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);

        var otvorene = await db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.ZakljucanaU == null)
            .ToListAsync(ct);

        // Pending više ne nastaje; ono što je ostalo iz vremena odobravanja je prihvaćeno.
        foreach (var o in otvorene.Where(o => o.Status == OrderStatus.Pending))
            o.TransitionTo(OrderStatus.Confirmed);

        var istekle = otvorene
            .Where(o => sada > SedmicaHelper.DatumIsporuke(o.Godina, o.Sedmica, dan, vrijemeOd).AddHours(-cutoffSati))
            .ToList();
        foreach (var o in istekle) o.ZakljucanaU = sada;

        await db.SaveChangesAsync(ct);

        // Vraćaju se samo sedmice čija isporuka tek dolazi — za njih se nabavlja. Prvi prolaz
        // poslije uvođenja zaključavanja nađe svu istoriju otvorenu; nju zaključa, ali bez
        // otkupnih listova za svaku prošlu sedmicu.
        return istekle
            .Select(o => new SedmicaKljuc(o.Godina, o.Sedmica))
            .Distinct()
            .Where(s => SedmicaHelper.DatumIsporuke(s.Godina, s.Sedmica, dan, vrijemeOd) >= sada)
            .OrderBy(s => s.Godina).ThenBy(s => s.Sedmica)
            .ToList();
    }
}
