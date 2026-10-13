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

    /// <summary>
    /// Cijena koju pretplatnik plaća kad zaradi popust. Odsutna je samo kad je pozivalac nije
    /// tražio. Računa je server (`PretplatnaCijena`), jer bi kopija tog množenja u pregledaču
    /// bila razlika između onoga što piše na ekranu i onoga što se naplati.
    /// </summary>
    public decimal? CijenaSaPretplatom { get; init; }

    /// <summary>Koliko isporuka po punoj cijeni prethodi popustu.</summary>
    public int? IsporukaDoPopusta { get; init; }

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

/// <summary>
/// Pakerska lista za jednu sedmicu. Nosi i broj narudžbi, jer prazna lista i „niko nije
/// naručio" izgledaju isto a znače različito — prije P2b je lista u toj situaciji pokazivala
/// sadržaj šablona, pa operater koji to pamti mora vidjeti nulu, ne prazninu.
/// </summary>
public record PakerskaListaDto
{
    public int BrojNarudzbi { get; init; }
    public IReadOnlyList<StavkaPakerskeDto> Stavke { get; init; } = [];
}

public record StavkaPakerskeDto
{
    public string Artikal { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public string Proizvodjac { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    /// <summary>Artikal je soft-obrisan (ili trajno nestao) poslije objave sadržaja koji
    /// ga sadrži. Red se i dalje prikazuje sa stvarnom količinom — pakerska lista je
    /// procurement istina i ne smije praviti prazne redove — ali ga operater mora
    /// posebno provjeriti umjesto da naruči "nepoznato".</summary>
    public bool ArtikalObrisan { get; init; }
}

/// <summary>Jedna korpica koju kupac trenutno može mijenjati.</summary>
public record MojaKorpicaDto
{
    public Guid OrderItemId { get; init; }
    public Guid OrderId { get; init; }
    public string KodNarudzbe { get; init; } = string.Empty;
    public string Naziv { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public DateTime RokIzmjene { get; init; }
    public decimal CiljnaVrijednost { get; init; }
    public decimal TrenutnaVrijednost { get; init; }
    public int BrojArtikala { get; init; }
    public int MinimalanBrojArtikala { get; init; }
}

public record StavkaMojeKorpiceDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public decimal JedinicnaCijena { get; init; }
    public bool DodaoKupac { get; init; }
}

/// <summary>Artikal koji kupac može dodati u korpicu.</summary>
public record PonudaArtiklaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public decimal Cijena { get; init; }
    public string? ImageUrl { get; init; }
    /// <summary>Artikal pripada kategorijama ove korpice. Birač takve stavlja prve.</summary>
    public bool IzOveKorpice { get; init; }
}

public record MojaKorpicaDetaljiDto
{
    public MojaKorpicaDto Korpica { get; init; } = new();
    public IReadOnlyList<StavkaMojeKorpiceDto> Stavke { get; init; } = [];
    public IReadOnlyList<PonudaArtiklaDto> Ponuda { get; init; } = [];
}
