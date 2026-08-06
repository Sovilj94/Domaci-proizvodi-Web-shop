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
