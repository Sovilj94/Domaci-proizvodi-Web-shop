using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using MediatR;

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
 */

public record GeocodeQuery(string Q) : IRequest<GeocodeHitDto?>;

public class GeocodeHandler(IGeocodingService geocoder) : IRequestHandler<GeocodeQuery, GeocodeHitDto?>
{
    public async Task<GeocodeHitDto?> Handle(GeocodeQuery q, CancellationToken ct)
    {
        // Kratak upit nema šta da nađe, a svaki poziv troši sekundu zajedničkog budžeta.
        if (q.Q.Trim().Length < 4) return null;
        return GeocodeHitDto.Od(await geocoder.GeocodeAsync(q.Q, ct));
    }
}

public record ReverseGeocodeQuery(double Lat, double Lon) : IRequest<GeocodeHitDto?>;

public class ReverseGeocodeHandler(IGeocodingService geocoder)
    : IRequestHandler<ReverseGeocodeQuery, GeocodeHitDto?>
{
    public async Task<GeocodeHitDto?> Handle(ReverseGeocodeQuery q, CancellationToken ct) =>
        GeocodeHitDto.Od(await geocoder.ReverseAsync(q.Lat, q.Lon, ct));
}
