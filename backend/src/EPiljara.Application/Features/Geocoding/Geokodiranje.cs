using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using MediatR;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Geocoding;

/*
 * Geokodiranje za pregledač. Postoji zato da picker adrese NE zove Nominatim direktno:
 * tako adresa kupca izlazi iz sistema na jednom mjestu, pod našim User-Agentom i našim
 * ograničenjem brzine, a ne sa IP adrese svakog kupca. Kad Nominatim postane sopstveni
 * (Nominatim:BaseUrl), adrese prestaju izlaziti uopšte — bez izmjene i jedne linije
 * ovdje ili u pregledaču.
 *
 * Endpointi su namjerno ANONIMNI: checkout koriste i gosti, bez naloga. Zaštita je
 * rate-limit particija "geocode" na kontroleru, da ovo ne postane otvoren relej.
 *
 * Svaki pogodak nosi `UPodrucjuDostave` (grad dostave iz Podesavanja), da pregledač kupcu
 * odmah kaže „ne dostavljamo na ovu adresu" umjesto da to sazna tek na kraju.
 */

internal static class Oznaci
{
    public static GeocodeHitDto? Pogodak(GeocodeHit? hit, DostavaOpcije dostava) =>
        hit is null
            ? null
            : GeocodeHitDto.Od(hit)! with
            {
                UPodrucjuDostave = PodrucjeDostave.Pripada(hit, dostava.GradDostave),
                GradDostave = dostava.GradDostave,
            };
}

public record GeocodeQuery(string Q) : IRequest<GeocodeHitDto?>;

public class GeocodeHandler(IGeocodingService geocoder, IOptions<DostavaOpcije> dostava)
    : IRequestHandler<GeocodeQuery, GeocodeHitDto?>
{
    public async Task<GeocodeHitDto?> Handle(GeocodeQuery q, CancellationToken ct)
    {
        // Kratak upit nema šta da nađe, a svaki poziv troši sekundu zajedničkog budžeta.
        if (q.Q.Trim().Length < 4) return null;
        return Oznaci.Pogodak(await geocoder.GeocodeAsync(q.Q, ct), dostava.Value);
    }
}

public record SuggestAddressQuery(string Q) : IRequest<IReadOnlyList<GeocodeHitDto>>;

public class SuggestAddressHandler(IAddressSuggester suggester, IOptions<DostavaOpcije> dostava)
    : IRequestHandler<SuggestAddressQuery, IReadOnlyList<GeocodeHitDto>>
{
    public async Task<IReadOnlyList<GeocodeHitDto>> Handle(SuggestAddressQuery q, CancellationToken ct)
    {
        // Dva slova pogađaju pola države; prijedlozi imaju smisla od trećeg.
        var upit = q.Q.Trim();
        if (upit.Length < 3) return [];
        if (upit.Length > 200) upit = upit[..200];
        var pogoci = await suggester.SuggestAsync(upit, ct);
        // Adrese u gradu dostave prve (stabilno — unutar grupe ostaje redoslijed geokodera);
        // ostale ostaju u listi, označene, da kupac vidi zašto „njegova" ulica ne prolazi.
        return pogoci
            .Select(h => Oznaci.Pogodak(h, dostava.Value)!)
            .OrderBy(d => d.UPodrucjuDostave == false)
            .ToList();
    }
}

public record ReverseGeocodeQuery(double Lat, double Lon) : IRequest<GeocodeHitDto?>;

public class ReverseGeocodeHandler(IGeocodingService geocoder, IOptions<DostavaOpcije> dostava)
    : IRequestHandler<ReverseGeocodeQuery, GeocodeHitDto?>
{
    public async Task<GeocodeHitDto?> Handle(ReverseGeocodeQuery q, CancellationToken ct) =>
        Oznaci.Pogodak(await geocoder.ReverseAsync(q.Lat, q.Lon, ct), dostava.Value);
}
