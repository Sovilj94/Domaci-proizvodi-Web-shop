using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// An authenticated principal. Role drives which JWT scope and endpoints apply
/// (Customer = web shop, Admin = admin panel, Driver = mobile driver app).
/// </summary>
public class User : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Guid? DeliveryZoneId { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Password accounts must confirm their email before they can log in.</summary>
    public bool EmailConfirmed { get; set; }
    /// <summary>One-time token e-mailed at registration; cleared once confirmed.</summary>
    public string? EmailConfirmationToken { get; set; }

    /// <summary>Lični kod za preporuke (do 8 znakova); null dok ga nalog ne zatraži.</summary>
    public string? PreporukaKod { get; set; }
    /// <summary>Ko je ovog kupca preporučio. SetNull: brisanje preporučioca ne briše kupca.</summary>
    public Guid? PreporucioId { get; set; }
}
