namespace EPiljara.Application.DTOs;

public record AuthUserDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    /// <summary>"customer" | "admin" | "driver".</summary>
    public string Role { get; init; } = "customer";
}

public record AuthResultDto
{
    public string Token { get; init; } = string.Empty;
    public AuthUserDto User { get; init; } = new();
}

/// <summary>Returned by registration — no token yet; the user must confirm their email first.</summary>
public record RegisterResultDto
{
    public string Email { get; init; } = string.Empty;
    public bool ConfirmationRequired { get; init; } = true;
}
