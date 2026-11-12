using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Voznje;

/*
 * Razduženje vozača. Naplata je pouzećem: za svaku isporučenu narudžbu vozač donosi gotovinu
 * (Order.TotalAmount, već umanjen za popuste i bodove). Za predati po danu = zbir isporučenih
 * narudžbi na rutama vozača tog dana. Predaju evidentira admin na redu DnevnaVoznja (jedan red po
 * vozaču i danu), pa ne dira gorivo ni kilometražu.
 */

public record RazduzenjeDanDto
{
    public Guid DriverId { get; init; }
    public string Vozac { get; init; } = string.Empty;
    public DateOnly Datum { get; init; }
    public int Dostavljeno { get; init; }
    public decimal ZaPredati { get; init; }
    public decimal? Predano { get; init; }
    public DateTime? PredanoU { get; init; }
    /// <summary>Predano − za predati: negativno = manjak. null dok predaja nije evidentirana.</summary>
    public decimal? Razlika { get; init; }
}

/// <summary>Koliko vozač još duguje za sve dane kad nije razdužen.</summary>
public record NerazduzenoDto
{
    public Guid DriverId { get; init; }
    public string Vozac { get; init; } = string.Empty;
    public decimal Iznos { get; init; }
    public int Dana { get; init; }
    public DateOnly OdDatuma { get; init; }
}

public record RazduzenjeDto
{
    public IReadOnlyList<RazduzenjeDanDto> Dani { get; init; } = [];
    public IReadOnlyList<NerazduzenoDto> Nerazduzeno { get; init; } = [];
}

internal static class Gotovina
{
    /// <summary>Nerazduženi dani se traže najviše ovoliko unazad.</summary>
    public const int NerazduzenoDanaUnazad = 120;

    public sealed record Dan(Guid DriverId, DateOnly Datum, int Dostavljeno, decimal Iznos);

    /// <summary>Isporučene narudžbe po vozaču i danu. Zbir se pravi u memoriji (SQLite ne sabira decimal).</summary>
    public static async Task<List<Dan>> DaniAsync(IAppDbContext db, DateOnly od, DateOnly doUklj, Guid? driverId, CancellationToken ct)
    {
        var redovi = await db.RouteStops.AsNoTracking()
            .Where(s => s.Route!.Date >= od && s.Route.Date <= doUklj
                        && (driverId == null || s.Route.DriverId == driverId)
                        && s.Order!.Status == OrderStatus.Delivered)
            .Select(s => new { s.Route!.DriverId, s.Route.Date, s.OrderId, s.Order!.TotalAmount })
            .ToListAsync(ct);

        return redovi
            .DistinctBy(r => r.OrderId)
            .GroupBy(r => (r.DriverId, r.Date))
            .Select(g => new Dan(g.Key.DriverId, g.Key.Date, g.Count(), g.Sum(r => r.TotalAmount)))
            .ToList();
    }

    public static async Task<Dictionary<(Guid, DateOnly), DnevnaVoznja>> PredajeAsync(
        IAppDbContext db, DateOnly od, DateOnly doUklj, Guid? driverId, CancellationToken ct) =>
        (await db.DnevneVoznje.AsNoTracking()
            .Where(v => v.Datum >= od && v.Datum <= doUklj && (driverId == null || v.DriverId == driverId))
            .ToListAsync(ct))
        .ToDictionary(v => (v.DriverId, v.Datum));

    public static RazduzenjeDanDto UDto(Dan d, string vozac, DnevnaVoznja? v) => new()
    {
        DriverId = d.DriverId, Vozac = vozac, Datum = d.Datum, Dostavljeno = d.Dostavljeno, ZaPredati = d.Iznos,
        Predano = v?.PredanoGotovine, PredanoU = v?.PredanoU,
        Razlika = v?.PredanoGotovine is decimal p ? p - d.Iznos : null,
    };

    /// <summary>Iznos koji vozač još nosi: svi dani sa isporukama bez evidentirane predaje.</summary>
    public static async Task<List<(Guid DriverId, decimal Iznos, int Dana, DateOnly OdDatuma)>> NerazduzenoAsync(
        IAppDbContext db, Guid? driverId, CancellationToken ct)
    {
        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        var od = danas.AddDays(-NerazduzenoDanaUnazad);
        var dani = await DaniAsync(db, od, danas, driverId, ct);
        var predaje = await PredajeAsync(db, od, danas, driverId, ct);
        return dani
            .Where(d => d.Iznos > 0 && predaje.GetValueOrDefault((d.DriverId, d.Datum))?.PredanoGotovine is null)
            .GroupBy(d => d.DriverId)
            .Select(g => (g.Key, g.Sum(d => d.Iznos), g.Count(), g.Min(d => d.Datum)))
            .ToList();
    }
}

/* ---------- Admin: pregled ---------- */

public record GetRazduzenjeQuery(DateOnly Od, DateOnly Do) : IRequest<RazduzenjeDto>;

public class GetRazduzenjeHandler(IAppDbContext db) : IRequestHandler<GetRazduzenjeQuery, RazduzenjeDto>
{
    public async Task<RazduzenjeDto> Handle(GetRazduzenjeQuery q, CancellationToken ct)
    {
        if (q.Do < q.Od) throw new DomainException("Kraj perioda je prije početka.", "period_neispravan");
        if (q.Do.DayNumber - q.Od.DayNumber > 92) throw new DomainException("Period može biti najviše tri mjeseca.", "period_predug");

        var dani = await Gotovina.DaniAsync(db, q.Od, q.Do, null, ct);
        var predaje = await Gotovina.PredajeAsync(db, q.Od, q.Do, null, ct);
        var nerazduzeno = await Gotovina.NerazduzenoAsync(db, null, ct);

        var ids = dani.Select(d => d.DriverId).Concat(nerazduzeno.Select(n => n.DriverId)).Distinct().ToList();
        var imena = await db.Drivers.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        return new RazduzenjeDto
        {
            Dani = dani
                .OrderByDescending(d => d.Datum).ThenBy(d => imena.GetValueOrDefault(d.DriverId))
                .Select(d => Gotovina.UDto(d, imena.GetValueOrDefault(d.DriverId) ?? string.Empty,
                    predaje.GetValueOrDefault((d.DriverId, d.Datum))))
                .ToList(),
            Nerazduzeno = nerazduzeno
                .OrderByDescending(n => n.Iznos)
                .Select(n => new NerazduzenoDto
                {
                    DriverId = n.DriverId, Vozac = imena.GetValueOrDefault(n.DriverId) ?? string.Empty,
                    Iznos = n.Iznos, Dana = n.Dana, OdDatuma = n.OdDatuma,
                })
                .ToList(),
        };
    }
}

/* ---------- Admin: evidencija predaje ---------- */

/// <summary>Iznos null poništava raniju evidenciju (npr. greškom upisano).</summary>
public record EvidentirajPredajuCommand(Guid DriverId, DateOnly Datum, decimal? Iznos) : IRequest<RazduzenjeDanDto>;

public record PredajaUnos(Guid DriverId, DateOnly Datum, decimal? Iznos);

public class EvidentirajPredajuValidator : AbstractValidator<EvidentirajPredajuCommand>
{
    public EvidentirajPredajuValidator()
    {
        RuleFor(x => x.Iznos).GreaterThanOrEqualTo(0).LessThan(1_000_000).When(x => x.Iznos is not null)
            .WithMessage("Iznos mora biti između 0 i 1.000.000 KM.");
    }
}

public class EvidentirajPredajuHandler(IAppDbContext db) : IRequestHandler<EvidentirajPredajuCommand, RazduzenjeDanDto>
{
    public async Task<RazduzenjeDanDto> Handle(EvidentirajPredajuCommand c, CancellationToken ct)
    {
        var vozac = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == c.DriverId, ct)
                    ?? throw new NotFoundException("Vozač", c.DriverId);

        var v = await db.DnevneVoznje.FirstOrDefaultAsync(x => x.DriverId == c.DriverId && x.Datum == c.Datum, ct);
        if (v is null)
        {
            if (c.Iznos is null) return await DanAsync(vozac, null, ct);
            // Procjena ture se popuni pri sljedećem osvježavanju (prazan potpis = zastarjela).
            v = new DnevnaVoznja { DriverId = c.DriverId, Datum = c.Datum };
            db.DnevneVoznje.Add(v);
        }

        v.PredanoGotovine = c.Iznos is decimal i ? Math.Round(i, 2) : null;
        v.PredanoU = c.Iznos is null ? null : DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await DanAsync(vozac, v, ct);

        async Task<RazduzenjeDanDto> DanAsync(Domain.Entities.Driver d, DnevnaVoznja? voznja, CancellationToken t)
        {
            var dan = (await Gotovina.DaniAsync(db, c.Datum, c.Datum, d.Id, t)).FirstOrDefault()
                      ?? new Gotovina.Dan(d.Id, c.Datum, 0, 0m);
            return Gotovina.UDto(dan, d.Name, voznja);
        }
    }
}

/* ---------- Admin: „Predao sve" ---------- */

/// <summary>Svi nerazduženi dani vozača do datuma (uključivo) postaju predati u punom iznosu. Dani sa
/// već evidentiranom predajom se ne diraju. Vraća broj razduženih dana.</summary>
public record RazduziSveCommand(Guid DriverId, DateOnly DoDatuma) : IRequest<int>;

public record RazduziSveUnos(Guid DriverId, DateOnly DoDatuma);

public class RazduziSveHandler(IAppDbContext db) : IRequestHandler<RazduziSveCommand, int>
{
    public async Task<int> Handle(RazduziSveCommand c, CancellationToken ct)
    {
        if (!await db.Drivers.AnyAsync(d => d.Id == c.DriverId, ct)) throw new NotFoundException("Vozač", c.DriverId);

        var dani = await Gotovina.DaniAsync(db, DateOnly.MinValue, c.DoDatuma, c.DriverId, ct);
        var postojece = await db.DnevneVoznje
            .Where(v => v.DriverId == c.DriverId && v.Datum <= c.DoDatuma)
            .ToDictionaryAsync(v => v.Datum, ct);
        var sada = DateTime.UtcNow;
        var broj = 0;
        foreach (var d in dani.Where(d => d.Iznos > 0))
        {
            if (!postojece.TryGetValue(d.Datum, out var v))
            {
                v = new DnevnaVoznja { DriverId = c.DriverId, Datum = d.Datum };
                db.DnevneVoznje.Add(v);
            }
            if (v.PredanoGotovine is not null) continue;
            v.PredanoGotovine = d.Iznos;
            v.PredanoU = sada;
            broj++;
        }
        await db.SaveChangesAsync(ct);
        return broj;
    }
}

/* ---------- Vozač: statistika za profil ---------- */

public record MojaStatistikaDto
{
    public int DostavljenoMjesec { get; init; }
    public int NeuspjeloMjesec { get; init; }
    /// <summary>Zbir pređenih km sa sata za dane ovog mjeseca kad su unesena oba stanja; null kad nijedan.</summary>
    public int? PredjenoKmMjesec { get; init; }
    public int DostavljenoDanas { get; init; }
    /// <summary>Gotovina naplaćena danas.</summary>
    public decimal GotovinaDanas { get; init; }
    /// <summary>Sva gotovina koju vozač još nije predao (svi nerazduženi dani).</summary>
    public decimal Nerazduzeno { get; init; }
}

public record GetMojaStatistikaQuery : IRequest<MojaStatistikaDto>;

public class GetMojaStatistikaHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetMojaStatistikaQuery, MojaStatistikaDto>
{
    public async Task<MojaStatistikaDto> Handle(GetMojaStatistikaQuery q, CancellationToken ct)
    {
        var vozac = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
                    ?? throw new ForbiddenException("Niste registrovani kao vozač.");
        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        var pocetak = new DateOnly(danas.Year, danas.Month, 1);
        var kraj = pocetak.AddMonths(1).AddDays(-1);

        var statusi = await db.RouteStops.AsNoTracking()
            .Where(s => s.Route!.DriverId == vozac.Id && s.Route.Date >= pocetak && s.Route.Date <= kraj
                        && (s.Order!.Status == OrderStatus.Delivered || s.Order.Status == OrderStatus.FailedDelivery))
            .Select(s => new { s.OrderId, s.Order!.Status })
            .ToListAsync(ct);
        var jedinstveni = statusi.DistinctBy(s => s.OrderId).ToList();

        var km = (await db.DnevneVoznje.AsNoTracking()
                .Where(v => v.DriverId == vozac.Id && v.Datum >= pocetak && v.Datum <= kraj)
                .ToListAsync(ct))
            .Select(v => v.PredjenoKm).OfType<int>().ToList();

        var danasDan = (await Gotovina.DaniAsync(db, danas, danas, vozac.Id, ct)).FirstOrDefault();
        var nerazduzeno = await Gotovina.NerazduzenoAsync(db, vozac.Id, ct);

        return new MojaStatistikaDto
        {
            DostavljenoMjesec = jedinstveni.Count(s => s.Status == OrderStatus.Delivered),
            NeuspjeloMjesec = jedinstveni.Count(s => s.Status == OrderStatus.FailedDelivery),
            PredjenoKmMjesec = km.Count == 0 ? null : km.Sum(),
            DostavljenoDanas = danasDan?.Dostavljeno ?? 0,
            GotovinaDanas = danasDan?.Iznos ?? 0m,
            Nerazduzeno = nerazduzeno.Sum(n => n.Iznos),
        };
    }
}
