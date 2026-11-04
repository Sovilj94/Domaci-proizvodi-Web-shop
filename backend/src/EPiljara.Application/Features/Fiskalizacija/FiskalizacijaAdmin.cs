using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Fiskalizacija;

public record FiskalniRacunRedDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public string KodNarudzbe { get; init; } = string.Empty;
    public string Kupac { get; init; } = string.Empty;
    public string Vrsta { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Iznos { get; init; }
    public DateTime Kreiran { get; init; }
    public DateTime? Izdat { get; init; }
    public string? Broj { get; init; }
    public int Pokusaja { get; init; }
    public string? ZadnjaGreska { get; init; }
    public bool TrajnaGreska { get; init; }
    public DateTime? SljedeciPokusaj { get; init; }
    /// <summary>Za prometni: postoji refundacija.</summary>
    public bool Refundiran { get; init; }
}

public record FiskalniRacunDto : FiskalniRacunRedDto
{
    public string NacinPlacanja { get; init; } = string.Empty;
    public string? Dobavljac { get; init; }
    public string? Brojac { get; init; }
    public string? QrSadrzaj { get; init; }
    public string? VerifikacijaUrl { get; init; }
    public string? RefundiraniBroj { get; init; }
    public IReadOnlyList<FiskalnaStavka> Stavke { get; init; } = [];
}

public record FiskalniPregledDto
{
    public DateOnly Od { get; init; }
    public DateOnly Do { get; init; }
    public IReadOnlyList<FiskalniRacunRedDto> Racuni { get; init; } = [];
    public int Izdato { get; init; }
    public decimal IznosPromet { get; init; }
    public decimal IznosRefundacija { get; init; }
    /// <summary>Svi neizdati, bez obzira na period — to je posao za danas.</summary>
    public int NaCekanju { get; init; }
    public int Greske { get; init; }
    public string Dobavljac { get; init; } = string.Empty;
    public bool Probni { get; init; }
}

public record FiskalnaPodesavanjaDto
{
    public bool PdvObveznik { get; init; }
    public decimal StopaPdv { get; init; }
    public string OznakaPdv { get; init; } = string.Empty;
    public string OznakaBezPdv { get; init; } = string.Empty;
    public string Dobavljac { get; init; } = string.Empty;
    public bool Probni { get; init; }
}

internal static class FiskalnaMapa
{
    public static IQueryable<FiskalniRacunRedDto> Redovi(IAppDbContext db, IQueryable<FiskalniRacun> q) =>
        q.Select(r => new FiskalniRacunRedDto
        {
            Id = r.Id, OrderId = r.OrderId, KodNarudzbe = r.Order!.Code, Kupac = r.Order.CustomerName,
            Vrsta = r.Vrsta.ToString(), Status = r.Status.ToString(), Iznos = r.Iznos, Kreiran = r.Kreiran, Izdat = r.Izdat,
            Broj = r.Broj, Pokusaja = r.Pokusaja, ZadnjaGreska = r.ZadnjaGreska, TrajnaGreska = r.TrajnaGreska,
            SljedeciPokusaj = r.SljedeciPokusaj,
            Refundiran = r.Vrsta == VrstaFiskalnogRacuna.Promet
                         && db.FiskalniRacuni.Any(x => x.OrderId == r.OrderId && x.Vrsta == VrstaFiskalnogRacuna.Refundacija),
        });
}

/// <summary>Računi nastali u periodu [Od, Do] (kalendarski dani, UTC) + sve što još čeka.</summary>
public record GetFiskalniRacuniQuery(DateOnly? Od = null, DateOnly? Do = null) : IRequest<FiskalniPregledDto>;

public class GetFiskalniRacuniHandler(IAppDbContext db, IFiskalniDobavljac dobavljac)
    : IRequestHandler<GetFiskalniRacuniQuery, FiskalniPregledDto>
{
    public async Task<FiskalniPregledDto> Handle(GetFiskalniRacuniQuery q, CancellationToken ct)
    {
        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        var od = q.Od ?? new DateOnly(danas.Year, danas.Month, 1);
        var @do = q.Do ?? danas;
        if (@do < od) throw new DomainException("Kraj perioda je prije početka.", "period_neispravan");
        if (@do.DayNumber - od.DayNumber > 400) throw new DomainException("Period može biti najviše godinu dana.", "period_predug");
        var pocetak = od.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var kraj = @do.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var uPeriodu = db.FiskalniRacuni.Where(r => r.Kreiran >= pocetak && r.Kreiran < kraj
                                                    || r.Status != StatusFiskalnogRacuna.Izdat);
        var racuni = await FiskalnaMapa.Redovi(db, uPeriodu).OrderByDescending(r => r.Kreiran).ToListAsync(ct);
        var izdati = racuni.Where(r => r.Status == nameof(StatusFiskalnogRacuna.Izdat)).ToList();

        return new FiskalniPregledDto
        {
            Od = od, Do = @do, Racuni = racuni,
            Izdato = izdati.Count,
            IznosPromet = izdati.Where(r => r.Vrsta == nameof(VrstaFiskalnogRacuna.Promet)).Sum(r => r.Iznos),
            IznosRefundacija = izdati.Where(r => r.Vrsta == nameof(VrstaFiskalnogRacuna.Refundacija)).Sum(r => r.Iznos),
            NaCekanju = racuni.Count(r => r.Status == nameof(StatusFiskalnogRacuna.NaCekanju)),
            Greske = racuni.Count(r => r.Status == nameof(StatusFiskalnogRacuna.Greska)),
            Dobavljac = dobavljac.Naziv,
            Probni = dobavljac.Probni,
        };
    }
}

public record GetFiskalniRacunQuery(Guid Id) : IRequest<FiskalniRacunDto>;

public class GetFiskalniRacunHandler(IAppDbContext db) : IRequestHandler<GetFiskalniRacunQuery, FiskalniRacunDto>
{
    public async Task<FiskalniRacunDto> Handle(GetFiskalniRacunQuery q, CancellationToken ct)
    {
        var r = await db.FiskalniRacuni.AsNoTracking().Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == q.Id, ct)
                ?? throw new NotFoundException("Fiskalni račun nije pronađen.");
        var red = await FiskalnaMapa.Redovi(db, db.FiskalniRacuni.Where(x => x.Id == q.Id)).SingleAsync(ct);
        var refundirani = r.RefundiraniRacunId is Guid rid
            ? await db.FiskalniRacuni.Where(x => x.Id == rid).Select(x => x.Broj).FirstOrDefaultAsync(ct)
            : null;
        return new FiskalniRacunDto
        {
            Id = red.Id, OrderId = red.OrderId, KodNarudzbe = red.KodNarudzbe, Kupac = red.Kupac, Vrsta = red.Vrsta,
            Status = red.Status, Iznos = red.Iznos, Kreiran = red.Kreiran, Izdat = red.Izdat, Broj = red.Broj,
            Pokusaja = red.Pokusaja, ZadnjaGreska = red.ZadnjaGreska, TrajnaGreska = red.TrajnaGreska,
            SljedeciPokusaj = red.SljedeciPokusaj, Refundiran = red.Refundiran,
            NacinPlacanja = r.NacinPlacanja, Dobavljac = r.Dobavljac, Brojac = r.Brojac, QrSadrzaj = r.QrSadrzaj,
            VerifikacijaUrl = r.VerifikacijaUrl, RefundiraniBroj = refundirani,
            Stavke = FiskalniSastav.Procitaj(r.StavkeJson),
        };
    }
}

/* ---------- podešavanja ---------- */

public record GetFiskalnaPodesavanjaQuery : IRequest<FiskalnaPodesavanjaDto>;

public class GetFiskalnaPodesavanjaHandler(IAppDbContext db, IFiskalniDobavljac dobavljac)
    : IRequestHandler<GetFiskalnaPodesavanjaQuery, FiskalnaPodesavanjaDto>
{
    public async Task<FiskalnaPodesavanjaDto> Handle(GetFiskalnaPodesavanjaQuery q, CancellationToken ct)
    {
        var p = await db.Podesavanja.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Podesavanja.JediniId, ct)
                ?? throw new InvalidOperationException("U bazi nema reda Podesavanja.");
        return new FiskalnaPodesavanjaDto
        {
            PdvObveznik = p.FiskalPdvObveznik, StopaPdv = p.FiskalStopaPdv, OznakaPdv = p.FiskalOznakaPdv,
            OznakaBezPdv = p.FiskalOznakaBezPdv, Dobavljac = dobavljac.Naziv, Probni = dobavljac.Probni,
        };
    }
}

public record SacuvajFiskalnaPodesavanjaCommand(bool PdvObveznik, decimal StopaPdv, string OznakaPdv, string OznakaBezPdv)
    : IRequest<FiskalnaPodesavanjaDto>;

public class SacuvajFiskalnaPodesavanjaValidator : AbstractValidator<SacuvajFiskalnaPodesavanjaCommand>
{
    public SacuvajFiskalnaPodesavanjaValidator()
    {
        RuleFor(x => x.StopaPdv).InclusiveBetween(0m, 50m).WithMessage("Stopa PDV-a mora biti od 0 do 50%.");
        RuleFor(x => x.OznakaPdv).NotEmpty().MaximumLength(10).WithMessage("Oznaka PDV-a je obavezna (do 10 znakova).");
        RuleFor(x => x.OznakaBezPdv).NotEmpty().MaximumLength(10).WithMessage("Oznaka „nije u PDV-u\" je obavezna (do 10 znakova).");
    }
}

public class SacuvajFiskalnaPodesavanjaHandler(IAppDbContext db, IFiskalniDobavljac dobavljac)
    : IRequestHandler<SacuvajFiskalnaPodesavanjaCommand, FiskalnaPodesavanjaDto>
{
    public async Task<FiskalnaPodesavanjaDto> Handle(SacuvajFiskalnaPodesavanjaCommand c, CancellationToken ct)
    {
        var p = await db.Podesavanja.FirstOrDefaultAsync(x => x.Id == Podesavanja.JediniId, ct)
                ?? throw new InvalidOperationException("U bazi nema reda Podesavanja.");
        p.FiskalPdvObveznik = c.PdvObveznik;
        p.FiskalStopaPdv = c.StopaPdv;
        p.FiskalOznakaPdv = c.OznakaPdv.Trim();
        p.FiskalOznakaBezPdv = c.OznakaBezPdv.Trim();
        await db.SaveChangesAsync(ct);
        return await new GetFiskalnaPodesavanjaHandler(db, dobavljac).Handle(new GetFiskalnaPodesavanjaQuery(), ct);
    }
}
