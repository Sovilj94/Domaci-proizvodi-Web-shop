namespace EPiljara.Application.Common.Interfaces;

/// <summary>
/// Pretvara tekstualnu adresu u koordinate. Implementacija mora poštovati
/// ograničenja servisa koji koristi (Nominatim: jedan zahtjev u sekundi).
/// </summary>
public interface IGeocodingService
{
    /// <summary>Vraća koordinate ili null kad adresa nije prepoznata.</summary>
    Task<(double Lat, double Lon)?> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}
