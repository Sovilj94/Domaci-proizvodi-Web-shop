using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// A delivery driver. Linked (optionally) to a <see cref="User"/> with role Driver
/// so the mobile driver app can authenticate and load its assigned route.
/// </summary>
public class Driver : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Da li vozač trenutno vozi. Neaktivan vozač nema tab na dispečerskom ekranu i ne
    /// može dobiti novu narudžbu; rute koje već ima ostaju netaknute, jer bi njihovo
    /// uklanjanje pojelo istoriju dana.
    /// </summary>
    public bool Aktivan { get; set; } = true;

    /// <summary>
    /// Koliko stanica vozač stigne u jednom danu. Ovo NIJE isto što i
    /// <c>AssignRouteHandler.MaxStanicaPoRuti</c> (10) — ono je tvrda granica Google Maps
    /// linka po ruti, ovo je dnevni kapacitet čovjeka i dijeli se na više ruta.
    /// </summary>
    public const int MaxStanicaPoDanu = 30;

    /// <summary>Vozilo kojim vozač obično dostavlja, npr. "Kombi 1". null dok se ne unese.</summary>
    public string? Vozilo { get; set; }
    /// <summary>Registarska oznaka vozila. null dok se ne unese.</summary>
    public string? Registracija { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public ICollection<DeliveryRoute> Routes { get; set; } = new List<DeliveryRoute>();
}
