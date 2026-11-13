using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Fiskalizacija;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/// <summary>
/// Fiskalni račun kako ga vozač štampa: tekst (žurnal) od dobavljača i sadržaj QR koda. Propis RS
/// traži da se račun izda u momentu prometa i kupcu preda štampan — kod nas je to na vratima, pa ga
/// štampa vozač na štampaču uz telefon.
/// </summary>
public record RacunStaniceDto
{
    public Guid Id { get; init; }
    /// <summary>Promet, Storno ili Refundacija.</summary>
    public string Vrsta { get; init; } = string.Empty;
    /// <summary>NaCekanju, Izdat ili Greska.</summary>
    public string Status { get; init; } = string.Empty;
    public decimal Iznos { get; init; }
    public string? Broj { get; init; }
    public DateTime? Izdat { get; init; }
    public string? Zurnal { get; init; }
    public string? QrSadrzaj { get; init; }
    public string? VerifikacijaUrl { get; init; }
    /// <summary>Greška dobavljača, da vozač zna zašto nema šta da štampa.</summary>
    public string? Greska { get; init; }
    /// <summary>Probni dobavljač: račun nema fiskalnu vrijednost.</summary>
    public bool Probni { get; init; }
}

/// <summary>
/// Računi narudžbe sa vozačeve rute, najstariji prvi. Prije čitanja ODMAH šalje dobavljaču račune te
/// narudžbe koji čekaju (inače bi vozač na vratima čekao posao od 30 s).
/// </summary>
public record GetRacuniStaniceQuery(Guid OrderId) : IRequest<IReadOnlyList<RacunStaniceDto>>;

public class GetRacuniStaniceHandler(IAppDbContext db, ICurrentUser current,
    IRequestHandler<ObradiFiskalneRacuneCommand, int> obrada, IFiskalniDobavljac dobavljac)
    : IRequestHandler<GetRacuniStaniceQuery, IReadOnlyList<RacunStaniceDto>>
{
    public async Task<IReadOnlyList<RacunStaniceDto>> Handle(GetRacuniStaniceQuery q, CancellationToken ct)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");
        var naRuti = await db.RouteStops.AnyAsync(s => s.OrderId == q.OrderId && s.Route!.DriverId == driver.Id, ct);
        if (!naRuti) throw new ForbiddenException("Ova narudžba nije na tvojoj ruti.");

        await obrada.Handle(new ObradiFiskalneRacuneCommand(OrderId: q.OrderId), ct);

        var racuni = await db.FiskalniRacuni.AsNoTracking()
            .Where(r => r.OrderId == q.OrderId)
            .OrderBy(r => r.Kreiran)
            .ToListAsync(ct);
        return racuni.Select(r => new RacunStaniceDto
        {
            Id = r.Id, Vrsta = r.Vrsta.ToString(), Status = r.Status.ToString(), Iznos = r.Iznos,
            Broj = r.Broj, Izdat = r.Izdat, Zurnal = r.Zurnal, QrSadrzaj = r.QrSadrzaj,
            VerifikacijaUrl = r.VerifikacijaUrl, Greska = r.ZadnjaGreska, Probni = dobavljac.Probni,
        }).ToList();
    }
}
