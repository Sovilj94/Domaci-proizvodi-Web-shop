using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Geokodiranje preko OpenStreetMap Nominatim-a. Njihova pravila korišćenja traže
/// najviše JEDAN zahtjev u sekundi i User-Agent koji identifikuje aplikaciju;
/// kršenje vodi u blokadu IP adrese. Razmak se čuva semaforom, pa je servis
/// bezbjedan i kad ga zove više poslova odjednom.
/// </summary>
public class NominatimGeocodingService(HttpClient http, ILogger<NominatimGeocodingService> logger)
    : IGeocodingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _zadnjiZahtjev = DateTimeOffset.MinValue;

    public async Task<(double Lat, double Lon)?> GeocodeAsync(
        string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var proteklo = DateTimeOffset.UtcNow - _zadnjiZahtjev;
            if (proteklo < TimeSpan.FromSeconds(1))
                await Task.Delay(TimeSpan.FromSeconds(1) - proteklo, cancellationToken);
            _zadnjiZahtjev = DateTimeOffset.UtcNow;

            var url = "https://nominatim.openstreetmap.org/search"
                + $"?format=jsonv2&limit=1&countrycodes=ba&q={Uri.EscapeDataString(address)}";

            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Nominatim vratio {Status} za adresu {Adresa}",
                    (int)response.StatusCode, address);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() == 0) return null;

            var hit = doc.RootElement[0];
            if (!double.TryParse(hit.GetProperty("lat").GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat)) return null;
            if (!double.TryParse(hit.GetProperty("lon").GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon)) return null;

            return (lat, lon);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Geokodiranje nije uspjelo za adresu {Adresa}", address);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }
}
