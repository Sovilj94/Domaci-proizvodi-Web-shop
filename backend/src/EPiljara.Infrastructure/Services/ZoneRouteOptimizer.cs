using EPiljara.Application.Common.Interfaces;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Rezervni optimizator rute: grupiše stanice po dostavnoj zoni u smislenom voznom
/// redoslijedu, čuvajući ulazni redoslijed unutar zone. Koristi se kad OSRM ne odgovori.
/// </summary>
public class ZoneRouteOptimizer : IRouteOptimizer
{
    private static readonly string[] ZoneOrder = ["Centar", "Borik", "Lauš", "Starčevica"];

    public Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
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

        // Sortiranje po zonama ne zna razdaljine ni puteve, pa nema šta da javi
        // o kilometraži, trajanju i liniji. Polazište se namjerno ignoriše.
        return Task.FromResult(new RouteOptimizationResult(ordered, null, null, null));
    }
}
