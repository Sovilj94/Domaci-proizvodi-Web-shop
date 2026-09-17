using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Sačuvana adresa dostave jednog kupca.
///
/// Narudžba NE pokazuje na ovaj red — ona čuva adresu kao tekst, isto kao što
/// OrderItem čuva cijenu u trenutku narudžbe. Zato brisanje adrese ne mijenja
/// nijednu raniju narudžbu, i zato ovdje namjerno nema veze ka Order.
/// </summary>
public class Address : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>„Kuća", „Posao"… Opciono; kupac ne mora imenovati adresu.</summary>
    public string? Label { get; set; }

    public string FullAddress { get; set; } = string.Empty;

    /// <summary>
    /// Nullable jer adrese prenesene iz User.Address nemaju pin i nikad ga nisu imale.
    /// Izmišljena tačka je gora od nikakve — vozača šalje na pogrešno mjesto sa punim
    /// povjerenjem, dok prazna tačka samo vrati rutu na sortiranje po zonama.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Guid? DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    /// <summary>Tačno jedna glavna po korisniku; održava se u handlerima.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
