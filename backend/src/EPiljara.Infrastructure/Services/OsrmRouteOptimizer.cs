using System.Text;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Računa redoslijed obilaska preko OSRM /trip endpointa, koji rješava problem
/// trgovačkog putnika i u istom odgovoru vraća kilometražu, trajanje i liniju rute.
///
/// Kad servis ne odgovori, vraća se na sortiranje po zonama — vozač dobije turu
/// koja radi, samo neoptimizovanu. Nikad prazan ekran zbog tuđeg servera.
/// </summary>
public class OsrmRouteOptimizer(
    HttpClient http,
    ZoneRouteOptimizer rezerva,
    ILogger<OsrmRouteOptimizer> logger) : IRouteOptimizer
{
    public async Task<RouteOptimizationResult> OptimizeAsync(
        double startLat,
        double startLon,
        IReadOnlyList<RouteStopInput> stops,
        CancellationToken cancellationToken = default)
    {
        // Optimizuju se samo stanice sa koordinatama; ostale idu na kraj.
        var saKoordinatama = stops
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .ToList();
        var bezKoordinata = stops
            .Where(s => !s.Latitude.HasValue || !s.Longitude.HasValue)
            .Select(s => s.OrderId)
            .ToList();

        // Ispod dvije tačke nema šta da se računa, i rezerva se namjerno NE zove:
        // po spec-u redoslijed tada ostaje onakav kakav ga je admin postavio.
        // Sortiranje po zonama je rezerva za neuspio poziv, ne za nedostatak podataka.
        if (saKoordinatama.Count < 2)
            return new RouteOptimizationResult(
                stops.Select(s => s.OrderId).ToList(), null, null, null);

        try
        {
            var tacke = new StringBuilder();
            tacke.Append(Broj(startLon)).Append(',').Append(Broj(startLat));
            foreach (var s in saKoordinatama)
                tacke.Append(';').Append(Broj(s.Longitude!.Value)).Append(',').Append(Broj(s.Latitude!.Value));

            var url = $"trip/v1/driving/{tacke}"
                + "?source=first&roundtrip=false&overview=simplified&geometries=geojson";

            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OSRM vratio {Status}", (int)response.StatusCode);
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetString() != "Ok")
                return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);

            // waypoints[0] je magacin; ostali nose waypoint_index — mjesto u obilasku.
            var waypoints = root.GetProperty("waypoints");
            var poredak = new List<(int Mjesto, Guid OrderId)>();
            for (var i = 1; i < waypoints.GetArrayLength(); i++)
            {
                var mjesto = waypoints[i].GetProperty("waypoint_index").GetInt32();
                poredak.Add((mjesto, saKoordinatama[i - 1].OrderId));
            }

            var redoslijed = poredak
                .OrderBy(x => x.Mjesto)
                .Select(x => x.OrderId)
                .Concat(bezKoordinata)
                .ToList();

            var trip = root.GetProperty("trips")[0];
            var duzina = trip.GetProperty("distance").GetDouble();
            var trajanje = trip.GetProperty("duration").GetDouble();
            var geometrija = trip.GetProperty("geometry").GetProperty("coordinates").GetRawText();

            return new RouteOptimizationResult(redoslijed, duzina, trajanje, geometrija);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning(e, "OSRM optimizacija nije uspjela, vraćam se na sortiranje po zonama");
            return await rezerva.OptimizeAsync(startLat, startLon, stops, cancellationToken);
        }
    }

    // OSRM očekuje tačku kao decimalni razdjelnik. Na srpskoj lokalizaciji
    // ToString() bi dao zarez i URL bi se raspao na pogrešnom mjestu.
    private static string Broj(double v) =>
        v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
}
