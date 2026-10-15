namespace EPiljara.Application.DTOs;

public record PromoKodDto
{
    public Guid Id { get; init; }
    public string Kod { get; init; } = "";
    public string Opis { get; init; } = "";
    public string Tip { get; init; } = "Procenat";
    public decimal Vrijednost { get; init; }
    public decimal? MinIznos { get; init; }
    public DateOnly? VaziOd { get; init; }
    public DateOnly? VaziDo { get; init; }
    public int? MaxKoristenja { get; init; }
    public bool JednomPoKupcu { get; init; }
    public bool SamoNoviKupci { get; init; }
    public bool Aktivan { get; init; }
    /// <summary>Neotkazane narudžbe sa ovim kodom — isti broj koji provjerava limit.</summary>
    public int BrojKoristenja { get; init; }
    /// <summary>Nijedna narudžba (ni otkazana) ne referencira kod — isto pravilo kao brisanje.</summary>
    public bool MozeSeObrisati { get; init; }
    public DateTime Kreiran { get; init; }
}

public record ProvjeraPromoKodaDto(string Kod, string Opis, decimal Popust, decimal Subtotal);
