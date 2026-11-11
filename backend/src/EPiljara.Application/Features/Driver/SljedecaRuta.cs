using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Driver;

/// <summary>
/// Vozač je zatvorio posljednju stanicu rute: od preostalih nepočetih ruta dana bira onu najbližu
/// njegovoj poziciji, računa joj stanice od te pozicije i stavlja je odmah iza početih/završenih.
/// Vraća null kad ruta više nema. Pozicija se, kao kod „Preračunaj od moje lokacije", koristi samo za
/// ovaj račun i nigdje se ne upisuje ni loguje (vidi ReoptimizeRouteHandler).
/// </summary>
public record SljedecaRutaCommand(double Latitude, double Longitude) : IRequest<DeliveryRouteDto?>;

public class SljedecaRutaHandler(IAppDbContext db, ICurrentUser current, IRouteOptimizer optimizer)
    : IRequestHandler<SljedecaRutaCommand, DeliveryRouteDto?>
{
    public async Task<DeliveryRouteDto?> Handle(SljedecaRutaCommand cmd, CancellationToken ct)
    {
        if (!GeoBounds.Contains(cmd.Latitude, cmd.Longitude))
            throw new DomainException(GeoBounds.OutsideMessage, "invalid_coordinates");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        var rute = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.DriverId == driver.Id && r.Date == danas)
            .ToListAsync(ct);

        var nepocete = rute.Where(r => r.Stops.Count > 0 && !RasporedDana.JePocela(r))
            .OrderBy(r => r.RedniBroj ?? int.MaxValue).ThenBy(r => r.Naziv)
            .ToList();
        if (nepocete.Count == 0) return null;

        var tacke = nepocete.Select(RasporedDana.Tacke).ToList();
        var izabrana = tacke.Any(t => t.Stanice.Count > 0)
            ? nepocete.First(r => r.Id == PlanRuta.Najbliza(tacke, cmd.Latitude, cmd.Longitude).Id)
            : nepocete[0];

        await Optimizacija.PrimijeniAsync(izabrana, cmd.Latitude, cmd.Longitude, optimizer, ct, odMagacina: false);

        // Izabrana dobija prvi broj iza početih i završenih; ostale nepočete se pomjeraju za jedan.
        var broj = rute.Where(RasporedDana.JePocela).Select(r => r.RedniBroj ?? 0).DefaultIfEmpty(0).Max();
        izabrana.RedniBroj = ++broj;
        foreach (var r in nepocete.Where(r => r != izabrana))
            r.RedniBroj = ++broj;

        await db.SaveChangesAsync(ct);
        return izabrana.ToDto();
    }
}
