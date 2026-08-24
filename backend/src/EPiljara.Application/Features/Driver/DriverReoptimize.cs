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
/// Pozicija se koristi samo za ovaj račun i nigdje se ne upisuje ni loguje.
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
        var route = await db.DeliveryRoutes
            .Include(r => r.Driver)
            .Include(r => r.Stops).ThenInclude(s => s.Order).ThenInclude(o => o!.DeliveryZone)
            .FirstOrDefaultAsync(r => r.DriverId == driver.Id && r.Date == danas, ct)
            ?? throw new NotFoundException("Nemate rutu za danas.");

        await Optimizacija.PrimijeniAsync(route, cmd.Latitude, cmd.Longitude, optimizer, ct);
        await db.SaveChangesAsync(ct);

        return route.ToDto();
    }
}
