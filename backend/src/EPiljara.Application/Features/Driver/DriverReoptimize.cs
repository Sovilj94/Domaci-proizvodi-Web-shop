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
/// Ponovo računa redoslijed preostalih stanica od mjesta gdje je vozač sada.
/// Pozicija se koristi samo za ovaj račun i nigdje se ne upisuje ni loguje: prosljeđuje se
/// u <c>IRouteOptimizer</c>, čiji HTTP klijent ima namjerno uklonjene podrazumijevane
/// loggere (vidi <c>RemoveAllLoggers()</c> u Infrastructure/DependencyInjection.cs) jer bi
/// inače ispisivali punu OSRM putanju sa koordinatama na Information nivou.
/// </summary>
public record ReoptimizeRouteCommand(double Latitude, double Longitude) : IRequest<DeliveryRouteDto>;

public class ReoptimizeRouteHandler(IAppDbContext db, ICurrentUser current, IRouteOptimizer optimizer)
    : IRequestHandler<ReoptimizeRouteCommand, DeliveryRouteDto>
{
    public async Task<DeliveryRouteDto> Handle(ReoptimizeRouteCommand cmd, CancellationToken ct)
    {
        if (!GeoBounds.Contains(cmd.Latitude, cmd.Longitude))
            throw new DomainException(GeoBounds.OutsideMessage, "invalid_coordinates");

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.UserId == current.Id, ct)
            ?? throw new ForbiddenException("Niste registrovani kao vozač.");

        var danas = DateOnly.FromDateTime(DateTime.UtcNow);
        // Isti poredak kao GET /driver/route — "prva" ruta mora biti determinisana otkad
        // jedan dan može imati više ruta (po zoni).
        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.DeliveryZone)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .Where(r => r.DriverId == driver.Id && r.Date == danas)
            .OrderBy(r => r.Naziv)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Nemate rutu za danas.");

        await Optimizacija.PrimijeniAsync(route, cmd.Latitude, cmd.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return route.ToDto();
    }
}
