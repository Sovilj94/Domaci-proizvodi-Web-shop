using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Orders;

/// <summary>
/// Narudžbe kojima status nije promijenjen do kraja perioda dostave njihove sedmice postaju
/// neuspjele dostave. Kraj perioda je kraj ZADNJEG termina sedmice (najveći dan + TimeTo iz
/// DeliverySlot-a), tumačen kao i ostala vremena termina. Bez termina u bazi ništa se ne radi.
/// Kupcu se ne šalje mejl — to je admin odluka poslije pregleda. Vraća broj označenih.
/// </summary>
public record OznaciNeisporuceneCommand(DateTime? Sada = null) : IRequest<int>;

public class OznaciNeisporuceneHandler(IAppDbContext db) : IRequestHandler<OznaciNeisporuceneCommand, int>
{
    private static readonly OrderStatus[] Nezavrsene =
        [OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.OutForDelivery];

    public async Task<int> Handle(OznaciNeisporuceneCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;
        var termini = await db.DeliverySlots.Select(s => new { s.DayOfWeek, s.TimeTo }).ToListAsync(ct);
        if (termini.Count == 0) return 0;

        var nezavrsene = await db.Orders.Where(o => Nezavrsene.Contains(o.Status)).ToListAsync(ct);
        var krajevi = new Dictionary<(int, int), DateTime>();
        DateTime Kraj(int g, int s)
        {
            if (!krajevi.TryGetValue((g, s), out var k))
                krajevi[(g, s)] = k = termini.Max(t => SedmicaHelper.DatumIsporuke(g, s, (DayOfWeek)t.DayOfWeek, t.TimeTo));
            return k;
        }

        var istekle = nezavrsene.Where(o => sada > Kraj(o.Godina, o.Sedmica)).ToList();
        foreach (var o in istekle) o.OznaciNeisporucenu();
        if (istekle.Count > 0) await db.SaveChangesAsync(ct);
        return istekle.Count;
    }
}

public record SedmicaRadaDto(int Godina, int Sedmica, DateTime Isporuka, DateTime Rok);

/// <summary>Sedmica koja se pakuje (posljednja zaključana) i sedmica koja je otvorena za narudžbe.</summary>
public record SedmiceRadaDto(SedmicaRadaDto Pakovanje, SedmicaRadaDto Otvorena);

public record GetSedmiceRadaQuery(DateTime? Sada = null) : IRequest<SedmiceRadaDto>;

public class GetSedmiceRadaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetSedmiceRadaQuery, SedmiceRadaDto>
{
    public async Task<SedmiceRadaDto> Handle(GetSedmiceRadaQuery q, CancellationToken ct)
    {
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        SedmicaRadaDto Dto((int G, int S) w)
        {
            var isporuka = SedmicaHelper.DatumIsporuke(w.G, w.S, dan, vrijemeOd);
            return new SedmicaRadaDto(w.G, w.S, isporuka, isporuka.AddHours(-cutoff));
        }
        return new SedmiceRadaDto(
            Dto(SedmicaHelper.ZaNabavku(dan, vrijemeOd, cutoff, q.Sada)),
            Dto(SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff, q.Sada)));
    }
}
