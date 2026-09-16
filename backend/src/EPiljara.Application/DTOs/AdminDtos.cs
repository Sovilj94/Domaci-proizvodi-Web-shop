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
    /// <summary>Iznos koji vozač naplaćuje pri dostavi. Platforma nema drugi način
    /// plaćanja — „Plaćanje pouzećem pri dostavi" je zakucano u OrderEmailTemplate i
    /// ViberMessage — pa je ukupan iznos narudžbe ujedno i pouzeće. Ako se ikad doda
    /// online plaćanje, kartica u aplikaciji vozača će tiho pokazivati pogrešan iznos
    /// za naplatu i ovo polje mora dobiti uslov.</summary>
    public decimal TotalAmount { get; init; }
}

public record DeliveryRouteDto
{
    public Guid Id { get; init; }
    /// <summary>ISO date "yyyy-MM-dd".</summary>
    public string Date { get; init; } = string.Empty;
    public Guid DriverId { get; init; }
    public string? DriverName { get; init; }
    public string Status { get; init; } = "Planned";
    /// <summary>Rezultat optimizacije; null kad je izostala.</summary>
    public double? DuzinaMetara { get; init; }
    public double? TrajanjeSekundi { get; init; }
    public string? Geometrija { get; init; }
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

/// <summary>
/// Polazište tura. Na žici engleski kao i ostali DTO-i, u domenu <c>Magacin</c> —
/// ime koje kod već koristi.
/// </summary>
public record WarehouseDto
{
    public string Name { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}
