namespace EPiljara.Application.DTOs;

public record DeliveryZoneDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal MinOrderAmount { get; init; }
    public decimal DeliveryFee { get; init; }
    public bool IsActive { get; init; }
}

public record DeliverySlotDto
{
    public Guid Id { get; init; }
    public int DayOfWeek { get; init; }
    public string TimeFrom { get; init; } = string.Empty;
    public string TimeTo { get; init; } = string.Empty;
    public int CutoffHours { get; init; }
    public string? Label { get; init; }
}

/// <summary>
/// Pogodak geokodiranja kakav pregledač dobije sa <c>/geocode</c>. Polja su ista koja
/// je picker ranije čitao direktno iz Nominatim odgovora (<c>display_name</c> i
/// kandidati za naselje), da prelazak na naš endpoint ne mijenja ponašanje ekrana.
/// </summary>
public record GeocodeHitDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<string> Localities { get; init; } = [];
    public string? Street { get; init; }
    public string? HouseNumber { get; init; }
    public string? City { get; init; }

    public static GeocodeHitDto? Od(EPiljara.Application.Common.Interfaces.GeocodeHit? hit) =>
        hit is null
            ? null
            : new GeocodeHitDto
            {
                Latitude = hit.Lat,
                Longitude = hit.Lon,
                DisplayName = hit.DisplayName,
                Localities = hit.Localities,
                Street = hit.Ulica,
                HouseNumber = hit.Broj,
                City = hit.Grad,
            };
}
