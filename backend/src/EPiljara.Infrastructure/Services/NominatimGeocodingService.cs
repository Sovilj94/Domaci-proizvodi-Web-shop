using System.Globalization;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Infrastructure.Services;

/// <summary>Podešavanja Nominatim klijenta, iz sekcije <c>Nominatim</c>.</summary>
public class NominatimOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Najmanji razmak između dva zahtjeva, u milisekundama. <b>1000 je obavezno za
    /// javnu instancu</b> — njihova pravila korišćenja traže najviše jedan zahtjev u
    /// sekundi, a kršenje vodi u blokadu IP adrese. Sopstvena instanca to ograničenje
    /// ne ima, pa se tamo postavlja na 0 (vidi <c>backend/nominatim/README.md</c>).
    /// </summary>
    public int ThrottleMs { get; set; } = 1000;
}

/// <summary>
/// Geokodiranje preko Nominatim-a. Adresa servisa dolazi iz <c>Nominatim:BaseUrl</c>
/// (isti obrazac kao <c>Osrm:BaseUrl</c>), pa se prelazak na sopstvenu instancu radi
/// bez ijedne izmjene koda.
///
/// Razmak između zahtjeva čuva semafor, pa je servis bezbjedan i kad ga zove više
/// poslova odjednom. Dok se koristi javna instanca, to znači da se SAV saobraćaj (i
/// pozadinski poslovi i pretrage iz pregledača kroz /geocode) provlači kroz jedan
/// zahtjev u sekundi; sa sopstvenom instancom <c>Nominatim:ThrottleMs</c> ide na 0 i
/// ograničenja nema.
/// </summary>
public class NominatimGeocodingService(
    HttpClient http,
    IOptions<NominatimOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _zadnjiZahtjev = DateTimeOffset.MinValue;

    public async Task<GeocodeHit?> GeocodeAsync(
        string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        // countrycodes=ba: pretraga se drži Bosne i Hercegovine, kao i GeoBounds provjera.
        var putanja = "search?format=jsonv2&addressdetails=1&limit=1&countrycodes=ba"
            + $"&accept-language=bs&q={Uri.EscapeDataString(address)}";

        return await PozoviAsync(putanja, prviIzNiza: true, opis: address, cancellationToken);
    }

    public async Task<GeocodeHit?> ReverseAsync(
        double lat, double lon, CancellationToken cancellationToken = default)
    {
        // InvariantCulture: srpska lokalizacija bi decimalnu tačku pretvorila u zapetu i
        // rasjekla URL na pogrešnom mjestu — ista zamka kao kod OSRM-a.
        var putanja = "reverse?format=jsonv2&addressdetails=1&accept-language=bs"
            + $"&lat={Broj(lat)}&lon={Broj(lon)}";

        return await PozoviAsync(putanja, prviIzNiza: false, opis: $"{Broj(lat)},{Broj(lon)}",
            cancellationToken);
    }

    private async Task<GeocodeHit?> PozoviAsync(
        string putanja, bool prviIzNiza, string opis, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var razmak = TimeSpan.FromMilliseconds(Math.Max(0, options.Value.ThrottleMs));
            var proteklo = DateTimeOffset.UtcNow - _zadnjiZahtjev;
            if (proteklo < razmak)
                await Task.Delay(razmak - proteklo, cancellationToken);
            _zadnjiZahtjev = DateTimeOffset.UtcNow;

            using var response = await http.GetAsync(putanja, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Namjerno bez adrese u logu: log se čuva i čita, a ovo je upravo podatak
                // koji cijela ova granica treba da drži unutra.
                logger.LogWarning("Nominatim vratio {Status}", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var hit = doc.RootElement;
            if (prviIzNiza)
            {
                if (hit.ValueKind != JsonValueKind.Array || hit.GetArrayLength() == 0) return null;
                hit = hit[0];
            }
            else if (hit.ValueKind != JsonValueKind.Object || hit.TryGetProperty("error", out _))
            {
                return null;
            }

            return Procitaj(hit);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Geokodiranje nije uspjelo ({Sta})", prviIzNiza ? "pretraga" : "reverse");
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static GeocodeHit? Procitaj(JsonElement hit)
    {
        if (!hit.TryGetProperty("lat", out var latEl) || !hit.TryGetProperty("lon", out var lonEl))
            return null;
        if (!double.TryParse(latEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!double.TryParse(lonEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            return null;

        var display = hit.TryGetProperty("display_name", out var d) ? d.GetString() ?? "" : "";
        return new GeocodeHit(lat, lon, display, Naselja(hit))
        {
            Ulica = Polje(hit, "road", "pedestrian", "footway", "square"),
            Broj = Polje(hit, "house_number"),
            Grad = Polje(hit, "city", "town", "village", "hamlet"),
        };
    }

    /// <summary>Prvo neprazno od navedenih polja iz <c>address</c>, ili null.</summary>
    private static string? Polje(JsonElement hit, params string[] kljucevi)
    {
        if (!hit.TryGetProperty("address", out var adresa) || adresa.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var k in kljucevi)
            if (adresa.TryGetProperty(k, out var v) && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString();
        return null;
    }

    /// <summary>
    /// Kandidati za ime naselja, po istom redoslijedu kao ranije u pregledaču, da
    /// pogađanje dostavne zone ostane nepromijenjeno.
    /// </summary>
    private static IReadOnlyList<string> Naselja(JsonElement hit)
    {
        if (!hit.TryGetProperty("address", out var adresa) || adresa.ValueKind != JsonValueKind.Object)
            return [];

        string[] kljucevi = ["quarter", "neighbourhood", "suburb", "city_district"];
        return kljucevi
            .Select(k => adresa.TryGetProperty(k, out var v) ? v.GetString() : null)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();
    }

    private static string Broj(double v) => v.ToString("0.#######", CultureInfo.InvariantCulture);
}
