using System.Globalization;
using System.Text.Json;
using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Infrastructure.Services;

/// <summary>Podešavanja Photon klijenta, iz sekcije <c>Photon</c>.</summary>
public class PhotonOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Tačka kojoj se prijedlozi „privlače" — iste ulice postoje u pola Balkana, a kupcu
    /// treba ona u gradu dostave. Ovo je samo pristrasnost rangiranja, ne filter.
    /// </summary>
    public double BiasLatitude { get; set; } = 44.7722;
    public double BiasLongitude { get; set; } = 17.191;
}

/// <summary>
/// Prijedlozi adresa preko Photon-a (komoot), geokodera nad OSM podacima napravljenog baš
/// za pretragu dok se kuca — što javni Nominatim izričito zabranjuje. Kao i Nominatim,
/// zove se samo sa servera: adresa kupca izlazi na ovom jednom mjestu, ne iz pregledača.
///
/// Rezultati se drže BiH (<see cref="GeoBounds"/> + <c>countrycode</c>), a iste ulice iz
/// više OSM komada (Photon vrati „Kralja Petra I Karađorđevića" tri puta) sažimaju se u jednu.
/// </summary>
public class PhotonAddressSuggester(
    HttpClient http,
    IOptions<PhotonOptions> options,
    ILogger<PhotonAddressSuggester> logger) : IAddressSuggester
{
    private const int Najvise = 5;

    public async Task<IReadOnlyList<GeocodeHit>> SuggestAsync(
        string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var pogoci = await TraziAsync(query, cancellationToken);

        // „Gundulićeva 10": kućni brojevi su u OSM-u rijetki, pa Photon tad nađe samo
        // neku drugu Gundulićevu 10 negdje u BiH. Dopuna je ista ulica bez broja (bliža
        // gradu dostave), sa upisanim brojem — tačka je na ulici, a kupac je dotjera na mapi.
        var (ulica, broj) = OdvojiBroj(query);
        if (broj is not null && pogoci.Count < Najvise && !cancellationToken.IsCancellationRequested)
        {
            var ulice = (await TraziAsync(ulica, cancellationToken))
                .Where(h => h.Broj is null)
                .Select(h => h with
                {
                    Broj = broj,
                    DisplayName = h.Grad is null ? $"{h.Ulica} {broj}" : $"{h.Ulica} {broj}, {h.Grad}",
                });
            // Stabilno: unutar „blizu" i „daleko" ostaje Photon-ov redoslijed, ali ulica u
            // gradu dostave ide ispred tačnog broja u drugom gradu.
            var o = options.Value;
            pogoci = pogoci
                .Concat(ulice)
                .DistinctBy(h => $"{h.Ulica}|{h.Broj}|{h.Grad}", StringComparer.OrdinalIgnoreCase)
                .OrderBy(h => Daleko(h, o.BiasLatitude, o.BiasLongitude))
                .Take(Najvise)
                .ToList();
        }

        return pogoci;
    }

    /// <summary>
    /// Grubo „van grada dostave": dalje od ~30 km (0.27° širine; dužina skraćena kosinusom
    /// geografske širine). Za poredak je dovoljno — ne treba prava udaljenost.
    /// </summary>
    private static bool Daleko(GeocodeHit h, double lat, double lon)
    {
        var dLat = h.Lat - lat;
        var dLon = (h.Lon - lon) * Math.Cos(lat * Math.PI / 180);
        return dLat * dLat + dLon * dLon > 0.27 * 0.27;
    }

    /// <summary>„Gundulićeva 10a" → („Gundulićeva", „10a"); bez broja na kraju → (upit, null).</summary>
    public static (string Ulica, string? Broj) OdvojiBroj(string upit)
    {
        var dijelovi = upit.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (dijelovi.Length < 2 || !char.IsDigit(dijelovi[^1][0])) return (upit.Trim(), null);
        return (string.Join(' ', dijelovi[..^1]), dijelovi[^1]);
    }

    private async Task<IReadOnlyList<GeocodeHit>> TraziAsync(string query, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var bbox = string.Join(',',
            Broj(GeoBounds.MinLongitude), Broj(GeoBounds.MinLatitude),
            Broj(GeoBounds.MaxLongitude), Broj(GeoBounds.MaxLatitude));
        // location_bias_scale/zoom: bez njih Photon blizinu gradu dostave jedva uzima u obzir,
        // pa „Jovana Dučića 20" iz Bileće stoji ispred one iz Banje Luke.
        var putanja = $"api/?q={Uri.EscapeDataString(query)}&limit=15&lang=default"
            + $"&lat={Broj(o.BiasLatitude)}&lon={Broj(o.BiasLongitude)}"
            + $"&location_bias_scale=0.1&zoom=12&bbox={bbox}";

        try
        {
            using var response = await http.GetAsync(putanja, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Bez upita u logu — to je adresa kupca.
                logger.LogWarning("Photon vratio {Status}", (int)response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return Procitaj(json);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Otkazan zahtjev je uobičajen (kupac kuca dalje) i nije vrijedan upozorenja.
            if (!cancellationToken.IsCancellationRequested)
                logger.LogWarning(e, "Prijedlozi adresa nisu uspjeli");
            return [];
        }
    }

    /// <summary>Čita Photon GeoJSON u pogotke. Javno radi testova.</summary>
    public static IReadOnlyList<GeocodeHit> Procitaj(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array)
            return [];

        var rezultat = new List<GeocodeHit>();
        var vidjeno = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in features.EnumerateArray())
        {
            if (!f.TryGetProperty("properties", out var p) || !f.TryGetProperty("geometry", out var g))
                continue;
            if (!string.Equals(Tekst(p, "countrycode"), "BA", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!g.TryGetProperty("coordinates", out var c) || c.ValueKind != JsonValueKind.Array
                || c.GetArrayLength() < 2)
                continue;

            var lon = c[0].GetDouble();
            var lat = c[1].GetDouble();
            if (!GeoBounds.Contains(lat, lon)) continue;

            var ime = Tekst(p, "name");
            var jeUlica = Tekst(p, "osm_key") == "highway";
            var ulica = jeUlica ? ime : Tekst(p, "street") ?? (Tekst(p, "housenumber") is null ? null : ime);
            var broj = Tekst(p, "housenumber");
            var grad = Tekst(p, "city") ?? Tekst(p, "town") ?? Tekst(p, "village") ?? Tekst(p, "county");

            // Bez ulice nema šta da se upiše u polje adrese (npr. pogodak je cijelo naselje).
            if (ulica is null) continue;

            var kljuc = $"{ulica}|{broj}|{grad}";
            if (!vidjeno.Add(kljuc)) continue;

            var ulicaIBroj = broj is null ? ulica : $"{ulica} {broj}";
            // Objekat (prodavnica, škola…) zadržava svoje ime ispred adrese — po njemu ga je
            // kupac i tražio.
            var dijelovi = new List<string?>
            {
                !jeUlica && ime is not null && ime != ulica ? ime : null,
                ulicaIBroj,
                grad,
            };
            var opis = string.Join(", ", dijelovi.Where(d => !string.IsNullOrWhiteSpace(d)));

            var naselja = new[] { Tekst(p, "district"), Tekst(p, "locality") }
                .Where(n => n is not null).Select(n => n!).ToList();

            rezultat.Add(new GeocodeHit(lat, lon, opis, naselja)
            {
                Ulica = ulica,
                Broj = broj,
                Grad = grad,
            });
            if (rezultat.Count == Najvise) break;
        }

        return rezultat;
    }

    private static string? Tekst(JsonElement p, string kljuc) =>
        p.TryGetProperty(kljuc, out var v) && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    private static string Broj(double v) => v.ToString("0.#######", CultureInfo.InvariantCulture);
}
