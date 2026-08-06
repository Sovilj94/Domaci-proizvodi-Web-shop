namespace EPiljara.Domain.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Packing,
    OutForDelivery,
    Delivered,
    FailedDelivery,
    Cancelled,
}

public enum RouteStatus
{
    Planned,
    Optimized,
    InProgress,
    Completed,
}

/// <summary>Sale unit. Serialized to the API as "kg" / "kom" / "l".</summary>
public enum Unit
{
    Kg,
    Kom,
    L,
}

public enum UserRole
{
    Customer,
    Admin,
    Driver,
}

public static class UnitExtensions
{
    public static string ToApi(this Unit unit) => unit switch
    {
        Unit.Kg => "kg",
        Unit.Kom => "kom",
        Unit.L => "l",
        _ => "kom",
    };

    public static Unit ParseUnit(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "kg" => Unit.Kg,
        "l" => Unit.L,
        _ => Unit.Kom,
    };
}
