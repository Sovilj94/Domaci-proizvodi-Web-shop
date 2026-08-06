using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>A recurring weekly delivery window (e.g. Thursday 16–20h).</summary>
public class DeliverySlot : Entity
{
    public int DayOfWeek { get; set; } // 0 = Sunday .. 6 = Saturday
    public string TimeFrom { get; set; } = string.Empty; // "16:00"
    public string TimeTo { get; set; } = string.Empty; // "20:00"
    public int CutoffHours { get; set; }
    public string? Label { get; set; }
}
