using EPiljara.Application.DTOs;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Mapping;

/// <summary>Hand-written entity → DTO projections (compile-safe, no runtime mapper).</summary>
public static class MappingExtensions
{
    public static CategoryDto ToDto(this Category c, int? productCount = null) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Slug = c.Slug,
        ImageUrl = c.ImageUrl,
        SortOrder = c.SortOrder,
        ProductCount = productCount,
    };

    public static ProducerDto ToDto(this Producer p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Village = p.Village,
        Description = p.Description,
        PhotoUrl = p.PhotoUrl,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        Phone = p.Phone,
    };

    public static ProducerRefDto ToRef(this Producer p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Village = p.Village,
        PhotoUrl = p.PhotoUrl,
    };

    public static ProductDto ToDto(this Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        Price = p.Price,
        Unit = p.Unit.ToApi(),
        Stock = p.Stock,
        IsAvailable = p.IsAvailable,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name ?? string.Empty,
        CategorySlug = p.Category?.Slug ?? string.Empty,
        ProducerId = p.ProducerId,
        Producer = p.Producer?.ToRef(),
        ImageUrl = p.ImageUrl,
        Description = p.Description,
        IsFeatured = p.IsFeatured,
    };

    public static DeliveryZoneDto ToDto(this DeliveryZone z) => new()
    {
        Id = z.Id,
        Name = z.Name,
        MinOrderAmount = z.MinOrderAmount,
        DeliveryFee = z.DeliveryFee,
        IsActive = z.IsActive,
    };

    public static DeliverySlotDto ToDto(this DeliverySlot s) => new()
    {
        Id = s.Id,
        DayOfWeek = s.DayOfWeek,
        TimeFrom = s.TimeFrom,
        TimeTo = s.TimeTo,
        CutoffHours = s.CutoffHours,
        Label = s.Label,
    };

    public static OrderItemDto ToDto(this OrderItem i) => new()
    {
        Id = i.Id,
        ProductId = i.ProductId,
        ProductName = i.ProductName,
        Unit = i.Unit.ToApi(),
        Quantity = i.Quantity,
        UnitPrice = i.UnitPrice,
        ImageUrl = i.ImageUrl,
    };

    public static OrderDto ToDto(this Order o) => new()
    {
        Id = o.Id,
        Code = o.Code,
        CustomerId = o.CustomerId,
        CustomerName = o.CustomerName,
        CustomerPhone = o.CustomerPhone,
        Status = o.Status.ToString(),
        Items = o.Items.Select(x => x.ToDto()).ToList(),
        Subtotal = o.Subtotal,
        DeliveryFee = o.DeliveryFee,
        TotalAmount = o.TotalAmount,
        DeliverySlotId = o.DeliverySlotId,
        DeliverySlot = o.DeliverySlot?.ToDto(),
        DeliveryZoneId = o.DeliveryZoneId,
        DeliveryZoneName = o.DeliveryZone?.Name,
        DeliveryAddress = o.DeliveryAddress,
        Note = o.Note,
        CreatedAt = o.CreatedAt,
    };

    public static DriverDto ToDto(this Driver d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Phone = d.Phone,
    };

    public static AuthUserDto ToAuthDto(this User u) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Phone = u.Phone,
        Address = u.Address,
        DeliveryZoneId = u.DeliveryZoneId,
        Role = u.Role.ToString().ToLowerInvariant(),
    };

    public static RouteStopDto ToDto(this RouteStop s) => new()
    {
        OrderId = s.OrderId,
        OrderCode = s.Order?.Code ?? string.Empty,
        CustomerName = s.Order?.CustomerName ?? string.Empty,
        Address = s.Order?.DeliveryAddress ?? string.Empty,
        ZoneName = s.Order?.DeliveryZone?.Name ?? string.Empty,
        Sequence = s.Sequence,
        CustomerPhone = s.Order?.CustomerPhone,
        Status = s.Order?.Status.ToString() ?? "Confirmed",
        Latitude = s.Order?.Latitude,
        Longitude = s.Order?.Longitude,
    };

    public static DeliveryRouteDto ToDto(this DeliveryRoute r) => new()
    {
        Id = r.Id,
        Date = r.Date.ToString("yyyy-MM-dd"),
        DriverId = r.DriverId,
        DriverName = r.Driver?.Name,
        Status = r.Status.ToString(),
        Stops = r.Stops.OrderBy(x => x.Sequence).Select(x => x.ToDto()).ToList(),
    };
}
