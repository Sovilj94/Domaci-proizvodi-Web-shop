namespace EPiljara.Application.DTOs;

public record DriverDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
}

public record RouteStopDto
{
    public Guid OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public int Sequence { get; init; }
    /// <summary>Included for the driver mobile app so it can call the customer.</summary>
    public string? CustomerPhone { get; init; }
    /// <summary>Order status, so the driver app knows which stops are done.</summary>
    public string Status { get; init; } = "Confirmed";
    /// <summary>Koordinate adrese dostave; null kad geokodiranje nije uspjelo.</summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public record DeliveryRouteDto
{
    public Guid Id { get; init; }
    /// <summary>ISO date "yyyy-MM-dd".</summary>
    public string Date { get; init; } = string.Empty;
    public Guid DriverId { get; init; }
    public string? DriverName { get; init; }
    public string Status { get; init; } = "Planned";
    public IReadOnlyList<RouteStopDto> Stops { get; init; } = [];
}

public record StatusCountDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public record DashboardDto
{
    public DateTime Date { get; init; }
    public int OrdersToday { get; init; }
    public decimal RevenueToday { get; init; }
    public int PendingOrders { get; init; }
    public int OutForDelivery { get; init; }
    public IReadOnlyList<StatusCountDto> StatusBreakdown { get; init; } = [];
}
