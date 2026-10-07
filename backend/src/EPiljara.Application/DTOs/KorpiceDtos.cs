namespace EPiljara.Application.DTOs;

public record KorpicaDto
{
    public Guid Id { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    /// <summary>Mala slova, bez dijakritike: povrce, voce, mlijecna, suhomesnata, domacinska.</summary>
    public string Tip { get; init; } = string.Empty;
    /// <summary>mala | srednja | velika</summary>
    public string Velicina { get; init; } = string.Empty;
    public decimal Cijena { get; init; }
    public string? ImageUrl { get; init; }
    public string Opis { get; init; } = string.Empty;
    public decimal Stock { get; init; }
    public bool Dostupna { get; init; }
}

public record StavkaSadrzajaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public string? ImageUrl { get; init; }
    public string ProizvodjacNaziv { get; init; } = string.Empty;
    public string ProizvodjacSelo { get; init; } = string.Empty;
}

public record SadrzajDto
{
    public Guid Id { get; init; }
    public Guid KorpicaId { get; init; }
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal CiljnaVrijednost { get; init; }
    public IReadOnlyList<StavkaSadrzajaDto> Stavke { get; init; } = [];
}

public record SedmicaDto
{
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public DateTime DatumIsporuke { get; init; }
    public DateTime RokNarucivanja { get; init; }
    public bool RokIstekao { get; init; }
}

public record SadrzajPregledDto
{
    /// <summary>null kad korpica za tu sedmicu još nema sadržaj.</summary>
    public Guid? SadrzajId { get; init; }
    public Guid KorpicaId { get; init; }
    public string KorpicaNaziv { get; init; } = string.Empty;
    public string Tip { get; init; } = string.Empty;
    public string Velicina { get; init; } = string.Empty;
    /// <summary>Nacrt | Objavljen | Zakljucan | Nema</summary>
    public string Status { get; init; } = "Nema";
    public int BrojArtikala { get; init; }
    public decimal Vrijednost { get; init; }
    public decimal CiljGenerisanja { get; init; }
}

public record StavkaPakerskeDto
{
    public string Artikal { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public string Proizvodjac { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
}
