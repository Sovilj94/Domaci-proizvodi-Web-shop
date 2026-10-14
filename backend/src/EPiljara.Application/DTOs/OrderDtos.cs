namespace EPiljara.Application.DTOs;

public record OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Unit { get; init; } = "kom";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string? ImageUrl { get; init; }
}

public record OrderDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public Guid? CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerPhone { get; init; }
    /// <summary>Adresa na koju je poslana potvrda. Ekran zahvale je ispisuje.</summary>
    public string? CustomerEmail { get; init; }
    public string Status { get; init; } = "Pending";
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
    public decimal Subtotal { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal TotalAmount { get; init; }
    public Guid DeliverySlotId { get; init; }
    public DeliverySlotDto? DeliverySlot { get; init; }
    public Guid DeliveryZoneId { get; init; }
    public string? DeliveryZoneName { get; init; }
    public string DeliveryAddress { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>Odsutno dok narudžba nije ocijenjena.</summary>
    public OcjenaDto? Ocjena { get; init; }
    /// <summary>Isporučena i još neocijenjena.</summary>
    public bool MozeSeOcijeniti { get; init; }
}

public record OcjenaDto
{
    public int Ocjena { get; init; }
    public string Komentar { get; init; } = string.Empty;
    public bool DozvolaZaObjavu { get; init; }
    public DateTime Kreirana { get; init; }
}
