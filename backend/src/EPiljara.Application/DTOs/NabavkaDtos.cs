namespace EPiljara.Application.DTOs;

/// <summary>Otkupni list jednog gazdinstva za jednu sedmicu isporuke.</summary>
public record OtkupniListDto
{
    public Guid Id { get; init; }
    public string Broj { get; init; } = string.Empty;
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public DateTime Datum { get; init; }
    /// <summary>Admin je poslije zaključavanja mijenjao narudžbe — količine možda više ne važe.</summary>
    public bool Zastario { get; init; }
    public string? Napomena { get; init; }
    public OtkupniListProdavacDto Prodavac { get; init; } = new();
    public IReadOnlyList<OtkupniListStavkaDto> Stavke { get; init; } = [];
    /// <summary>Σ naručeno × cijena, samo preko stavki sa cijenom; null kad nijedna nema cijenu.</summary>
    public decimal? Iznos { get; init; }
    /// <summary>Σ primljeno × cijena preko stavki sa cijenom i unesenim primljenim; null kad nema nijedne.</summary>
    public decimal? IznosPrimljeno { get; init; }
}

/// <summary>Gazdinstvo kao prodavac na otkupnom listu — samo za admina (sadrži JMBG i račun).</summary>
public record OtkupniListProdavacDto
{
    public Guid Id { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    public string? Telefon { get; init; }
    public string? NosilacIme { get; init; }
    public string? Bpg { get; init; }
    public string? Jmbg { get; init; }
    public string? Adresa { get; init; }
    public string? ZiroRacun { get; init; }
    public string? Banka { get; init; }
}

public record OtkupniListStavkaDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public decimal Naruceno { get; init; }
    public decimal? Cijena { get; init; }
    public decimal? Primljeno { get; init; }
}
