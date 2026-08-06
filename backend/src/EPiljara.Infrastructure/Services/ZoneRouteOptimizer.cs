using EPiljara.Application.Common.Interfaces;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Phase-1 route optimization: group stops by delivery zone in a sensible driving order,
/// preserving input order within a zone. A Google Routes-backed implementation of
/// <see cref="IRouteOptimizer"/> (true VRP) can replace this in Phase 3 with no other changes.
/// </summary>
public class ZoneRouteOptimizer : IRouteOptimizer
{
    private static readonly string[] ZoneOrder = ["Centar", "Borik", "Lauš", "Starčevica"];

    public Task<IReadOnlyList<Guid>> OptimizeAsync(
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
        var ordered = stops
            .Select((stop, index) => (stop, index))
            .OrderBy(x =>
            {
                var idx = Array.IndexOf(ZoneOrder, x.stop.ZoneName);
                return idx < 0 ? int.MaxValue : idx;
            })
            .ThenBy(x => x.index)
            .Select(x => x.stop.OrderId)
            .ToList();

        return Task.FromResult<IReadOnlyList<Guid>>(ordered);
    }
}
