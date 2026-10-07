using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Prebacuje objavljene sadržaje kojima je rok naručivanja prošao u Zakljucan.
/// Od tog trenutka je pakerska lista zamrznuta. <paramref name="Sada"/> postoji
/// radi testova; u radu ostaje null.
/// </summary>
public record ZakljucajSadrzajeCommand(DateTime? Sada = null) : IRequest<int>;

public class ZakljucajSadrzajeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<ZakljucajSadrzajeCommand, int>
{
    public async Task<int> Handle(ZakljucajSadrzajeCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;

        var kandidati = await db.SedmicniSadrzaji
            .Where(s => s.Status == StatusSadrzaja.Objavljen)
            .ToListAsync(ct);

        var zakljucani = 0;
        foreach (var s in kandidati)
        {
            var rok = SedmicaHelper.DatumIsporuke(s.Godina, s.Sedmica)
                .AddHours(-opcije.Value.RokSati);
            if (sada <= rok) continue;

            s.Predji(StatusSadrzaja.Zakljucan);
            zakljucani++;
        }

        if (zakljucani > 0) await db.SaveChangesAsync(ct);
        return zakljucani;
    }
}
