namespace EPiljara.Application.DTOs;

public class AddressDto
{
    public Guid Id { get; init; }
    public string? Label { get; init; }
    public string FullAddress { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    public string? DeliveryZoneName { get; init; }

    /// <summary>
    /// Zona se može ugasiti nakon što je adresa sačuvana. Klijent to mora znati da
    /// ne bi predlagao zonu koju CreateOrderValidator odbija sa `invalid_zone` tek
    /// pri slanju narudžbe.
    /// </summary>
    public bool DeliveryZoneActive { get; init; }

    public bool IsDefault { get; init; }
}

/// <summary>
/// Jedna pretplata na ekranu naloga. Cijene su izvedene (vidi PretplatnaCijena) i računaju
/// se pri svakom čitanju, jer zavise od broja dosadašnjih isporuka, ne od trenutka pretplate.
/// </summary>
public class PretplataDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;

    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Tip { get; init; }
    public string? Velicina { get; init; }
    public string? ImageUrl { get; init; }

    public string DeliveryAddress { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Guid DeliveryZoneId { get; init; }
    public string? DeliveryZoneName { get; init; }

    /// <summary>Isti razlog kao kod adrese: zona se može ugasiti poslije pretplate.</summary>
    public bool DeliveryZoneActive { get; init; }

    public Guid DeliverySlotId { get; init; }
    public string? DeliverySlotLabel { get; init; }
    public string? Napomena { get; init; }

    public int BrojIsporuka { get; init; }
    public decimal PunaCijena { get; init; }

    /// <summary>Cijena naredne isporuke — umanjena tek kad je popust zarađen.</summary>
    public decimal TrenutnaCijena { get; init; }
    public bool PopustAktivan { get; init; }

    /// <summary>Koliko još isporuka do niže cijene; 0 kad je kupac već ima.</summary>
    public int DoPopusta { get; init; }

    /// <summary>
    /// Sedmica naredne isporuke. Za pauziranu pretplatu je to prva sedmica poslije pauze,
    /// a za otkazanu je nema.
    /// </summary>
    public int? SljedecaGodina { get; init; }
    public int? SljedecaSedmica { get; init; }
    public DateTime? SljedecaIsporuka { get; init; }

    public int? PauzaDoGodina { get; init; }
    public int? PauzaDoSedmica { get; init; }
    public DateTime CreatedAt { get; init; }
}
