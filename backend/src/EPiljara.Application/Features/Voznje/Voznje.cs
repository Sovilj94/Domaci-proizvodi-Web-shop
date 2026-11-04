using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Voznje;

public record VoznjaDto
{
    public Guid Id { get; init; }
    public Guid DriverId { get; init; }
    public string Vozac { get; init; } = string.Empty;
    public DateOnly Datum { get; init; }
    public int Stanica { get; init; }
    public int BezKoordinata { get; init; }
    /// <summary>Procijenjeni km dnevne ture sa povratkom u magacin; null kad nema koordinata.</summary>
    public double? Km { get; init; }
    public int? TrajanjeMin { get; init; }
    /// <summary>"osrm" ili "vazdusno".</summary>
    public string? Izvor { get; init; }
    public decimal? CijenaPoLitru { get; init; }
    public decimal? PotrosnjaL100km { get; init; }
    public decimal? ProcjenaLitara { get; init; }
    /// <summary>Km × potrošnja × cijena važeća na taj dan; null bez km ili bez cijene goriva.</summary>
    public decimal? ProcjenaGoriva { get; init; }
    public decimal? PlacenoGorivo { get; init; }
    public decimal? Litara { get; init; }
    public string? Napomena { get; init; }
    public string? PlacenoUnio { get; init; }
    public DateTime? PlacenoUneseno { get; init; }
    /// <summary>(plaćeno − procjena) / procjena, u procentima.</summary>
    public decimal? OdstupanjeProcenat { get; init; }
    /// <summary>|odstupanje| ≥ <see cref="VoznjeMapa.BitnoOdstupanje"/>%.</summary>
    public bool BitnoOdstupanje { get; init; }
}

public static class VoznjeMapa
{
    /// <summary>Od ovog procenta odstupanja plaćenog od procjene, admin dobija upozorenje.</summary>
    public const decimal BitnoOdstupanje = 20m;

    public static VoznjaDto UDto(DnevnaVoznja v, string vozac, IEnumerable<CijenaGoriva> cijene)
    {
        var km = v.ProcjenaMetara is double m ? Math.Round(m / 1000, 1) : (double?)null;
        var c = DnevnaVoznjaProcjena.CijenaZa(cijene, v.Datum);
        decimal? procjena = km is double k && c is not null ? Math.Round(DnevnaVoznjaProcjena.TrosakKm(k, c), 2) : null;
        decimal? odstupanje = procjena is > 0 && v.PlacenoGorivo is decimal p
            ? Math.Round((p - procjena.Value) / procjena.Value * 100m, 1)
            : null;
        return new VoznjaDto
        {
            Id = v.Id, DriverId = v.DriverId, Vozac = vozac, Datum = v.Datum,
            Stanica = v.ProcjenaStanica, BezKoordinata = v.ProcjenaBezKoordinata,
            Km = km, TrajanjeMin = v.ProcjenaSekundi is double s ? (int)Math.Round(s / 60) : null, Izvor = v.ProcjenaIzvor,
            CijenaPoLitru = c?.CijenaPoLitru, PotrosnjaL100km = c?.PotrosnjaL100km,
            ProcjenaLitara = km is double kl && c is not null ? Math.Round((decimal)kl * c.PotrosnjaL100km / 100m, 1) : null,
            ProcjenaGoriva = procjena,
            PlacenoGorivo = v.PlacenoGorivo, Litara = v.Litara, Napomena = v.Napomena,
            PlacenoUnio = v.PlacenoUnio, PlacenoUneseno = v.PlacenoUneseno,
            OdstupanjeProcenat = odstupanje,
            BitnoOdstupanje = odstupanje is decimal o && Math.Abs(o) >= BitnoOdstupanje,
        };
    }

    /// <summary>
    /// Osvježi procjene za dane [od, do] (svi vozači ili jedan) i vrati redove. Jedan upit za rute,
    /// jedan za postojeće redove, jedno snimanje.
    /// </summary>
    public static async Task<List<DnevnaVoznja>> OsvjeziOpsegAsync(
        IAppDbContext db, IRouteOptimizer optimizer, DateOnly od, DateOnly doUklj, Guid? driverId, CancellationToken ct)
    {
        var magacin = await MagacinCitac.UcitajAsync(db, ct);
        var rute = await DnevnaVoznjaProcjena.RuteAsync(db, od, doUklj, driverId, ct);
        var postojece = await db.DnevneVoznje
            .Where(v => v.Datum >= od && v.Datum <= doUklj && (driverId == null || v.DriverId == driverId))
            .ToListAsync(ct);

        var kljucevi = rute.Select(r => (r.DriverId, r.Date))
            .Concat(postojece.Select(v => (v.DriverId, Date: v.Datum)))
            .Distinct()
            .ToList();
        var sada = DateTime.UtcNow;
        var rezultat = new List<DnevnaVoznja>();
        foreach (var (vozac, datum) in kljucevi)
        {
            var v = await DnevnaVoznjaProcjena.OsvjeziAsync(db, optimizer, magacin, vozac, datum,
                rute.Where(r => r.DriverId == vozac && r.Date == datum).ToList(),
                postojece.FirstOrDefault(x => x.DriverId == vozac && x.Datum == datum), sada, ct);
            if (v is not null) rezultat.Add(v);
        }
        await db.SaveChangesAsync(ct);
        return rezultat;
    }

    public static async Task<List<VoznjaDto>> UDtoAsync(IAppDbContext db, IEnumerable<DnevnaVoznja> voznje, CancellationToken ct)
    {
        var lista = voznje.ToList();
        var ids = lista.Select(v => v.DriverId).Distinct().ToList();
        var imena = await db.Drivers.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var cijene = await db.CijeneGoriva.AsNoTracking().ToListAsync(ct);
        return lista
            .OrderBy(v => v.Datum).ThenBy(v => imena.GetValueOrDefault(v.DriverId))
            .Select(v => UDto(v, imena.GetValueOrDefault(v.DriverId) ?? string.Empty, cijene))
            .ToList();
    }
}

/* ---------- Admin: dan (dispečerski ekran) ---------- */

/// <summary>Procjena svakog vozača za dan; zastarjele (promijenjen skup stanica) se preračunaju odmah.</summary>
public record GetVoznjeDanaQuery(DateOnly Datum) : IRequest<IReadOnlyList<VoznjaDto>>;

public class GetVoznjeDanaHandler(IAppDbContext db, IRouteOptimizer optimizer)
    : IRequestHandler<GetVoznjeDanaQuery, IReadOnlyList<VoznjaDto>>
{
    public async Task<IReadOnlyList<VoznjaDto>> Handle(GetVoznjeDanaQuery q, CancellationToken ct) =>
        await VoznjeMapa.UDtoAsync(db,
            await VoznjeMapa.OsvjeziOpsegAsync(db, optimizer, q.Datum, q.Datum, null, ct), ct);
}

/* ---------- Admin: mjesec (stranica Troškovi) ---------- */

/// <summary>Sve vožnje mjeseca, bez preračuna (to radi pozadinski posao) — pregled je samo čitanje.</summary>
public record GetVoznjeMjesecaQuery(int Godina, int Mjesec) : IRequest<IReadOnlyList<VoznjaDto>>;

public class GetVoznjeMjesecaHandler(IAppDbContext db) : IRequestHandler<GetVoznjeMjesecaQuery, IReadOnlyList<VoznjaDto>>
{
    public async Task<IReadOnlyList<VoznjaDto>> Handle(GetVoznjeMjesecaQuery q, CancellationToken ct)
    {
        if (q.Mjesec is < 1 or > 12) throw new DomainException("Mjesec mora biti od 1 do 12.", "mjesec_neispravan");
        var od = new DateOnly(q.Godina, q.Mjesec, 1);
        var @do = od.AddMonths(1);
        var voznje = await db.DnevneVoznje.AsNoTracking().Where(v => v.Datum >= od && v.Datum < @do).ToListAsync(ct);
        return await VoznjeMapa.UDtoAsync(db, voznje, ct);
    }
}

/* ---------- Unos goriva sa pumpe ---------- */

public record GorivoUnos(decimal? Iznos, decimal? Litara, string? Napomena);

internal static class GorivoPravila
{
    public static void Validiraj<T>(AbstractValidator<T> v, Func<T, decimal?> iznos, Func<T, decimal?> litara, Func<T, string?> napomena)
    {
        v.RuleFor(x => iznos(x)).InclusiveBetween(0.01m, 2000m).When(x => iznos(x) is not null)
            .WithMessage("Iznos mora biti između 0,01 i 2.000 KM.").OverridePropertyName("Iznos");
        v.RuleFor(x => litara(x)).InclusiveBetween(0.1m, 500m).When(x => litara(x) is not null)
            .WithMessage("Litara mora biti između 0,1 i 500.").OverridePropertyName("Litara");
        v.RuleFor(x => napomena(x)).MaximumLength(300).WithMessage("Napomena može imati najviše 300 znakova.")
            .OverridePropertyName("Napomena");
    }

    /// <summary>Iznos null briše unos (dan se tada opet računa po procjeni).</summary>
    public static void Upisi(DnevnaVoznja v, decimal? iznos, decimal? litara, string? napomena, string unio)
    {
        if (iznos is null)
        {
            v.PlacenoGorivo = null;
            v.Litara = null;
            v.Napomena = null;
            v.PlacenoUneseno = null;
            v.PlacenoUnio = null;
            return;
        }
        v.PlacenoGorivo = Math.Round(iznos.Value, 2);
        v.Litara = litara is decimal l ? Math.Round(l, 2) : null;
        v.Napomena = string.IsNullOrWhiteSpace(napomena) ? null : napomena.Trim();
        v.PlacenoUneseno = DateTime.UtcNow;
        v.PlacenoUnio = unio;
    }
}

public record SacuvajGorivoVoznjeCommand(Guid Id, decimal? Iznos, decimal? Litara, string? Napomena) : IRequest<VoznjaDto>;

public class SacuvajGorivoVoznjeValidator : AbstractValidator<SacuvajGorivoVoznjeCommand>
{
    public SacuvajGorivoVoznjeValidator() => GorivoPravila.Validiraj(this, x => x.Iznos, x => x.Litara, x => x.Napomena);
}

public class SacuvajGorivoVoznjeHandler(IAppDbContext db) : IRequestHandler<SacuvajGorivoVoznjeCommand, VoznjaDto>
{
    public async Task<VoznjaDto> Handle(SacuvajGorivoVoznjeCommand c, CancellationToken ct)
    {
        var v = await db.DnevneVoznje.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                ?? throw new NotFoundException("Vožnja nije pronađena.");
        GorivoPravila.Upisi(v, c.Iznos, c.Litara, c.Napomena, "admin");
        await db.SaveChangesAsync(ct);
        return (await VoznjeMapa.UDtoAsync(db, [v], ct))[0];
    }
}

/* ---------- Vozač (Android aplikacija) ---------- */

/// <summary>Vozačev dan: procjena (preračunata ako je zastarjela) i njegov unos goriva. null kad tog dana nema stanica.</summary>
public record GetMojaVoznjaQuery(DateOnly? Datum) : IRequest<VoznjaDto?>;

public class GetMojaVoznjaHandler(IAppDbContext db, ICurrentUser current, IRouteOptimizer optimizer)
    : IRequestHandler<GetMojaVoznjaQuery, VoznjaDto?>
{
    public async Task<VoznjaDto?> Handle(GetMojaVoznjaQuery q, CancellationToken ct)
    {
        var vozac = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
                    ?? throw new ForbiddenException("Niste registrovani kao vozač.");
        var datum = q.Datum ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var voznje = await VoznjeMapa.OsvjeziOpsegAsync(db, optimizer, datum, datum, vozac.Id, ct);
        return voznje.Count == 0 ? null : (await VoznjeMapa.UDtoAsync(db, voznje, ct))[0];
    }
}

public record SacuvajMojeGorivoCommand(DateOnly Datum, decimal? Iznos, decimal? Litara, string? Napomena) : IRequest<VoznjaDto>;

public class SacuvajMojeGorivoValidator : AbstractValidator<SacuvajMojeGorivoCommand>
{
    public SacuvajMojeGorivoValidator() => GorivoPravila.Validiraj(this, x => x.Iznos, x => x.Litara, x => x.Napomena);
}

public class SacuvajMojeGorivoHandler(IAppDbContext db, ICurrentUser current, IRouteOptimizer optimizer, IEmailService email)
    : IRequestHandler<SacuvajMojeGorivoCommand, VoznjaDto>
{
    /// <summary>Vozač može unijeti ili ispraviti gorivo za danas i ovoliko dana unazad; starije ispravlja admin.</summary>
    public const int DanaUnazad = 7;

    public async Task<VoznjaDto> Handle(SacuvajMojeGorivoCommand c, CancellationToken ct)
    {
        var vozac = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
                    ?? throw new ForbiddenException("Niste registrovani kao vozač.");
        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        // +1: vozač u Bosni je do 1–2 h ispred UTC-a, pa mu je „danas" ponekad UTC-ovo „sutra".
        if (c.Datum > danas.AddDays(1))
            throw new DomainException("Gorivo se unosi za današnji ili raniji dan.", "datum_u_buducnosti");
        if (c.Datum < danas.AddDays(-DanaUnazad))
            throw new DomainException($"Gorivo stariji od {DanaUnazad} dana ispravlja admin.", "datum_prestar");

        var voznje = await VoznjeMapa.OsvjeziOpsegAsync(db, optimizer, c.Datum, c.Datum, vozac.Id, ct);
        var v = voznje.FirstOrDefault()
                ?? throw new DomainException("Taj dan nemate dostava.", "nema_voznje");
        GorivoPravila.Upisi(v, c.Iznos, c.Litara, c.Napomena, "vozac");
        await db.SaveChangesAsync(ct);
        var dto = (await VoznjeMapa.UDtoAsync(db, [v], ct))[0];
        if (dto is { BitnoOdstupanje: true, ProcjenaGoriva: decimal procjena, PlacenoGorivo: decimal placeno, OdstupanjeProcenat: decimal odst })
            await Mejlovi.ObavjestenjaPravila.OdstupanjeGorivaAsync(db, email, vozac.Name, c.Datum, procjena, placeno, odst, dto.Napomena, ct);
        return dto;
    }
}

/* ---------- Pozadinski posao ---------- */

/// <summary>Osvježava procjene od prije par dana do dvije sedmice unaprijed, da analitika ima broj i kad niko ne otvori ekran.</summary>
public record OsvjeziProcjeneVoznjiCommand(DateTime? Sada = null) : IRequest<int>;

public class OsvjeziProcjeneVoznjiHandler(IAppDbContext db, IRouteOptimizer optimizer)
    : IRequestHandler<OsvjeziProcjeneVoznjiCommand, int>
{
    public async Task<int> Handle(OsvjeziProcjeneVoznjiCommand c, CancellationToken ct)
    {
        var danas = DateOnly.FromDateTime(c.Sada ?? DateTime.UtcNow);
        return (await VoznjeMapa.OsvjeziOpsegAsync(db, optimizer, danas.AddDays(-3), danas.AddDays(14), null, ct)).Count;
    }
}
