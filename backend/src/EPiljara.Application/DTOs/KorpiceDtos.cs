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
    /// Cijene po planu pretplate (R2). Odsutne su samo kad ih pozivalac nije tražio. Računa ih
    /// server (`PretplatnaCijena`), jer bi kopija tog množenja u pregledaču bila razlika između
    /// onoga što piše na ekranu i onoga što se naplati.
    /// </summary>
    public decimal? CijenaNedjeljna { get; init; }
    public decimal? CijenaMjesecna { get; init; }
    public decimal? CijenaTriMjeseca { get; init; }
    /// <summary>Obaveza mjesečnog i tromjesečnog plana, u isporukama.</summary>
    public int? ObavezaMjesecna { get; init; }
    public int? ObavezaTriMjeseca { get; init; }

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
    public string? IzrezUrl { get; init; }
    /// <summary>Boja pločice namirnice (BojaProizvoda).</summary>
    public string Boja { get; init; } = string.Empty;
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
    /// <summary>
    /// Javni pogled: sezonske kulture koje kupac može uzeti umjesto nečega iz kutije pri izmjeni
    /// sadržaja (isti kandidati kao „Zamijeni", samo kategorije ove korpice). Admin pogled je prazan.
    /// </summary>
    public IReadOnlyList<DodatnaKulturaDto> Dodatne { get; init; } = [];
}

public record DodatnaKulturaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public string? ImageUrl { get; init; }
    public string? IzrezUrl { get; init; }
    public string Boja { get; init; } = string.Empty;
    public string ProizvodjacNaziv { get; init; } = string.Empty;
    public string ProizvodjacSelo { get; init; } = string.Empty;
}

/// <summary>
/// Sadržaj za admin editor: javni <see cref="SadrzajDto"/> plus cijene, nabavne cijene,
/// marža i predložene zamjene. Odvojen tip — ništa od ovoga ne smije na javni API.
/// </summary>
public record AdminSadrzajDto : SadrzajDto
{
    public AdminSadrzajDto(SadrzajDto osnova) : base(osnova) { }

    public decimal CijenaKorpice { get; init; }
    public decimal Vrijednost { get; init; }
    /// <summary>Σ(količina × trenutna nabavna cijena) za stavke kojima je nabavna unesena.</summary>
    public decimal NabavnaVrijednost { get; init; }
    /// <summary>Marža korpice prema njenoj cijeni (0–1). Null kad nijedna stavka nema nabavnu cijenu.</summary>
    public decimal? Marza { get; init; }
    /// <summary>Najviše koliko sadržaj smije koštati u nabavci (cijena × (1 − minimalna marža)).</summary>
    public decimal NabavniBudzet { get; init; }
    public decimal MinimalnaMarza { get; init; }
    public IReadOnlyList<CijenaStavkeDto> Cijene { get; init; } = [];
    public IReadOnlyList<ZamjenaDto> Zamjene { get; init; } = [];
}

public record CijenaStavkeDto
{
    public Guid ProductId { get; init; }
    public decimal JedinicnaCijena { get; init; }
    public decimal? NabavnaCijena { get; init; }
}

public record ZamjenaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public decimal Cijena { get; init; }
    public decimal? NabavnaCijena { get; init; }
    public string? ImageUrl { get; init; }
}

public record SedmicaDto
{
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public DateTime DatumIsporuke { get; init; }
    public DateTime RokNarucivanja { get; init; }
    public bool RokIstekao { get; init; }

    /// <summary>
    /// Isporuka i rok NAREDNE sedmice. Poslije roka checkout prodaje za narednu sedmicu
    /// (SedmicaHelper.SedmicaZaNarudzbu), pa odbrojavanje na početnoj tada broji do ovoga.
    /// </summary>
    public DateTime SljedecaIsporuka { get; init; }
    public DateTime SljedeciRok { get; init; }
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
    public decimal CijenaKorpice { get; init; }
    /// <summary>Σ(količina × trenutna nabavna cijena), samo stavke sa nabavnom cijenom.</summary>
    public decimal NabavnaVrijednost { get; init; }
    /// <summary>Marža korpice (0–1). Null kad sadržaja nema ili nijedna stavka nema nabavnu cijenu.</summary>
    public decimal? Marza { get; init; }
    public int BezNabavneCijene { get; init; }
    public int BrojZamjena { get; init; }
}

/// <summary>
/// Pakerska lista za jednu sedmicu. Nosi i broj narudžbi, jer prazna lista i „niko nije
/// naručio" izgledaju isto a znače različito — prije P2b je lista u toj situaciji pokazivala
/// sadržaj šablona, pa operater koji to pamti mora vidjeti nulu, ne prazninu.
/// </summary>
public record PakerskaListaDto
{
    public int BrojNarudzbi { get; init; }
    /// <summary>Trošak nabavke cijele liste po trenutnim nabavnim cijenama (bez artikala bez nabavne).</summary>
    public decimal NabavnaVrijednost { get; init; }
    public int BezNabavneCijene { get; init; }
    public IReadOnlyList<StavkaPakerskeDto> Stavke { get; init; } = [];
}

public record StavkaPakerskeDto
{
    public string Artikal { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public string Proizvodjac { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    public decimal? NabavnaCijena { get; init; }
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
    /// <summary>Predložena zamjena iz sedmičnog sadržaja — birač zamjene je nudi prvu.</summary>
    public bool PreporucenaZamjena { get; init; }
}

public record MojaKorpicaDetaljiDto
{
    public MojaKorpicaDto Korpica { get; init; } = new();
    public IReadOnlyList<StavkaMojeKorpiceDto> Stavke { get; init; } = [];
    public IReadOnlyList<PonudaArtiklaDto> Ponuda { get; init; } = [];
    /// <summary>Kupčev spisak „ne stavljaj mi" — birač zamjene ih ne nudi.</summary>
    public IReadOnlyList<Guid> Izbjegnuti { get; init; } = [];
}

public record IzbjegnutArtikalDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
}

/// <summary>Kupčeva korpica u jednoj narudžbi, kako je admin vidi i uređuje.</summary>
public record AdminKorpicaNarudzbeDto
{
    public Guid OrderItemId { get; init; }
    public string KodNarudzbe { get; init; } = string.Empty;
    public string StatusNarudzbe { get; init; } = string.Empty;
    public string Kupac { get; init; } = string.Empty;
    public string Korpica { get; init; } = string.Empty;
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    /// <summary>Koliko kutija ide u ovoj stavci narudžbe (količine ispod su po kutiji).</summary>
    public decimal BrojKutija { get; init; }
    public decimal CijenaKorpice { get; init; }
    /// <summary>Cilj vrijednosti iz objavljenog sadržaja; 0 kad sadržaja nema.</summary>
    public decimal Cilj { get; init; }
    public decimal Vrijednost { get; init; }
    public decimal NabavnaVrijednost { get; init; }
    public decimal? Marza { get; init; }
    /// <summary>Narudžba još nije krenula u dostavu — admin smije mijenjati sadržaj.</summary>
    public bool MozeSeMijenjati { get; init; }
    public IReadOnlyList<AdminStavkaKorpiceNarudzbeDto> Stavke { get; init; } = [];
    public IReadOnlyList<IzbjegnutArtikalDto> Izbjegnuti { get; init; } = [];
}

public record AdminStavkaKorpiceNarudzbeDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public decimal Kolicina { get; init; }
    public decimal JedinicnaCijena { get; init; }
    public decimal? NabavnaCijena { get; init; }
    /// <summary>Artikal nije iz sedmičnog sadržaja — dodao ga je kupac, admin ili zamjena.</summary>
    public bool Izmijenjeno { get; init; }
}
