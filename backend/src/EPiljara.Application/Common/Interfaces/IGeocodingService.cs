namespace EPiljara.Application.Common.Interfaces;

/// <summary>
/// Jedan pogodak geokodiranja: tačka, čitljiv opis adrese, i imena naselja/kvartova
/// koja se koriste za pogađanje dostavne zone.
/// </summary>
public record GeocodeHit(
    double Lat,
    double Lon,
    string DisplayName,
    IReadOnlyList<string> Localities);

/// <summary>
/// Pretvara adresu u koordinate i obrnuto. Implementacija mora poštovati ograničenja
/// servisa koji koristi (javni Nominatim: jedan zahtjev u sekundi).
///
/// Cijelo geokodiranje ide kroz ovaj interfejs, i samo kroz njega — i pozadinski
/// poslovi i zahtjevi iz pregledača (preko <c>/geocode</c>). To je jedina granica na
/// kojoj adrese kupaca izlaze iz sistema, pa je i jedino mjesto koje treba zamijeniti
/// da prestanu izlaziti.
/// </summary>
public interface IGeocodingService
{
    /// <summary>Vraća pogodak ili null kad adresa nije prepoznata.</summary>
    Task<GeocodeHit?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>Vraća adresu za datu tačku ili null kad je nema.</summary>
    Task<GeocodeHit?> ReverseAsync(double lat, double lon, CancellationToken cancellationToken = default);
}
