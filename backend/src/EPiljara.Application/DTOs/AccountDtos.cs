namespace EPiljara.Application.DTOs;

public class AddressDto
{
    public Guid Id { get; init; }
    public string? Label { get; init; }
    public string FullAddress { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    public string? DeliveryZoneName { get; init; }

    /// <summary>
    /// Zona se može ugasiti nakon što je adresa sačuvana. Klijent to mora znati da
    /// ne bi predlagao zonu koju CreateOrderValidator odbija sa `invalid_zone` tek
    /// pri slanju narudžbe.
    /// </summary>
    public bool DeliveryZoneActive { get; init; }

    public bool IsDefault { get; init; }
}
