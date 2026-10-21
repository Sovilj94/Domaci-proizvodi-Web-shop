namespace EPiljara.Application.DTOs;

public record DriverDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    /// <summary>Neaktivan vozač nema tab na dispečerskom ekranu i ne prima nove narudžbe.</summary>
    public bool Aktivan { get; init; } = true;
    /// <summary>Vozilo kojim vozač obično dostavlja, npr. „Kombi 1". null dok se ne unese.</summary>
    public string? Vozilo { get; init; }
    /// <summary>Registarska oznaka vozila. null dok se ne unese.</summary>
    public string? Registracija { get; init; }
    /// <summary>Dnevni kapacitet vozača u stanicama; puni traku kapaciteta na dispečerskom
    /// ekranu. Konstanta (<see cref="Domain.Entities.Driver.MaxStanicaPoDanu"/>), ne kolona —
    /// ide u DTO da ga front ne bi zakucao kod sebe.</summary>
    public int MaxStanica { get; init; } = Domain.Entities.Driver.MaxStanicaPoDanu;
}

/// <summary>Profil prijavljenog vozača za mobilnu aplikaciju (ime, kontakt, vozilo).</summary>
public record DriverProfileDto
{
    public string Name { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    /// <summary>Vozilo kojim vozač dostavlja; null dok admin ne unese podatak.</summary>
    public string? Vozilo { get; init; }
    /// <summary>Registarska oznaka vozila; null dok admin ne unese podatak.</summary>
    public string? Registracija { get; init; }
}

/// <summary>Jedna prošla (završena) ruta u istoriji vozača.</summary>
public record RouteHistoryEntryDto
{
    public Guid Id { get; init; }
    /// <summary>ISO datum "yyyy-MM-dd".</summary>
    public string Datum { get; init; } = string.Empty;
    public int UkupnoStanica { get; init; }
    public int Isporuceno { get; init; }
    public int Neuspjelo { get; init; }
    /// <summary>Kad je prva stanica na ruti zatvorena (UTC); null ako nijedna nije zatvorena.</summary>
    public DateTime? PrvaZatvorenaU { get; init; }
    /// <summary>Kad je posljednja stanica na ruti zatvorena (UTC); null ako nijedna nije zatvorena.</summary>
    public DateTime? PosljednjaZatvorenaU { get; init; }
}

/// <summary>Istorija ruta prijavljenog vozača — prošle rute i agregati za tekući mjesec.</summary>
public record DriverRouteHistoryDto
{
    /// <summary>Isporučene stanice u tekućem mjesecu.</summary>
    public int UkupnoIsporuka { get; init; }
    /// <summary>Procenat uspješnosti u tekućem mjesecu: isporučene / (isporučene + neuspjele).</summary>
    public int Uspjesnost { get; init; }
    /// <summary>Zbir DuzinaMetara ruta u tekućem mjesecu; null ako nijedna ruta nema poznatu dužinu.</summary>
    public double? PredjenoMetara { get; init; }
    public IReadOnlyList<RouteHistoryEntryDto> Rute { get; init; } = [];
}

public record RouteStopDto
{
    public Guid OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public int Sequence { get; init; }
    /// <summary>Included for the driver mobile app so it can call the customer.</summary>
    public string? CustomerPhone { get; init; }
    /// <summary>Order status, so the driver app knows which stops are done.</summary>
    public string Status { get; init; } = "Confirmed";
    /// <summary>Koordinate adrese dostave; null kad geokodiranje nije uspjelo.</summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    /// <summary>Iznos koji vozač naplaćuje pri dostavi. Platforma nema drugi način
    /// plaćanja — „Plaćanje pouzećem pri dostavi" je zakucano u OrderEmailTemplate i
    /// ViberMessage — pa je ukupan iznos narudžbe ujedno i pouzeće. Ako se ikad doda
    /// online plaćanje, kartica u aplikaciji vozača će tiho pokazivati pogrešan iznos
    /// za naplatu i ovo polje mora dobiti uslov.</summary>
    public decimal TotalAmount { get; init; }
    /// <summary>Kad je vozač zatvorio stanicu (UTC); null dok je otvorena. Dispečerski
    /// ekran time prigušuje isporučene narudžbe i ispisuje vrijeme isporuke.</summary>
    public DateTime? ZatvorenaU { get; init; }
}

public record DeliveryRouteDto
{
    public Guid Id { get; init; }
    /// <summary>ISO date "yyyy-MM-dd".</summary>
    public string Date { get; init; } = string.Empty;
    public Guid DriverId { get; init; }
    public string? DriverName { get; init; }
    public string Status { get; init; } = "Planned";
    /// <summary>Rezultat optimizacije; null kad je izostala.</summary>
    public double? DuzinaMetara { get; init; }
    public double? TrajanjeSekundi { get; init; }
    public string? Geometrija { get; init; }
    public IReadOnlyList<RouteStopDto> Stops { get; init; } = [];
    /// <summary>Naziv koji vozač vidi na kartici — naziv zone, uz redni broj ("Centar 1")
    /// kad je zona podijeljena na više ruta.</summary>
    public string Naziv { get; init; } = string.Empty;
    /// <summary>Sirovi naziv zone bez rednog broja; null kad ruta nema zonu ("Bez zone").</summary>
    public string? ZonaNaziv { get; init; }
}

public record StatusCountDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>Jedan zadatak na početnom ekranu admina. Nivo: hitno | upozorenje | info.</summary>
public record ZadatakDto
{
    public string Kljuc { get; init; } = string.Empty;
    public string Nivo { get; init; } = "info";
    public string Naslov { get; init; } = string.Empty;
    public string Opis { get; init; } = string.Empty;
    public int Broj { get; init; }
    public string Link { get; init; } = string.Empty;
    public string Akcija { get; init; } = string.Empty;
}

public record AdminZadaciDto
{
    /// <summary>Sedmica koju checkout upravo prodaje — za nju se gleda sadržaj korpica.</summary>
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public IReadOnlyList<ZadatakDto> Zadaci { get; init; } = [];
    /// <summary>Brojke uz stavke menija: narudzbe, korpice, proizvodi, partneri.</summary>
    public IReadOnlyDictionary<string, int> Meni { get; init; } = new Dictionary<string, int>();
}

public record DashboardDto
{
    public DateTime Date { get; init; }
    public int OrdersToday { get; init; }
    public decimal RevenueToday { get; init; }
    public int PendingOrders { get; init; }
    public int OutForDelivery { get; init; }
    public IReadOnlyList<StatusCountDto> StatusBreakdown { get; init; } = [];
}

/// <summary>
/// Polazište tura. Na žici engleski kao i ostali DTO-i, u domenu <c>Magacin</c> —
/// ime koje kod već koristi.
/// </summary>
public record WarehouseDto
{
    public string Name { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

/// <summary>
/// Pretplate za nabavku: spisak plus dva broja koja su smisao ekrana — koliko je korpica
/// obećano za narednu sedmicu i koliko njih još nema objavljen sadržaj.
/// </summary>
public class AdminPretplateDto
{
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public int BrojAktivnih { get; init; }
    public int CekajuSadrzaj { get; init; }
    public IReadOnlyList<AdminPretplataDto> Stavke { get; init; } = [];
}

public class AdminPretplataDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Kupac { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Telefon { get; init; }
    public string Korpica { get; init; } = string.Empty;
    /// <summary>"Nedjeljna" | "Mjesecna" | "TriMjeseca" (R2).</summary>
    public string Plan { get; init; } = "Nedjeljna";
    public int PreostaloObaveze { get; init; }
    public decimal Cijena { get; init; }
    public int BrojIsporuka { get; init; }
    public string? Zona { get; init; }
    public string Adresa { get; init; } = string.Empty;
    public int? PoslednjaGodina { get; init; }
    public int? PoslednjaSedmica { get; init; }

    /// <summary>Ima li ova korpica objavljen sadržaj za ciljnu sedmicu.</summary>
    public bool SadrzajSpreman { get; init; }
    /// <summary>A2: zadnja sedmica za koju je kupac dobio mejl „ove sedmice nema isporuke".</summary>
    public int? ObavijestPreskocenaGodina { get; init; }
    public int? ObavijestPreskocenaSedmica { get; init; }
}

/// <summary>`GET /admin/pakovanje` — sedmica razložena po kutiji.</summary>
public record PakovanjeDto
{
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public IReadOnlyList<PakovanjeNarudzbaDto> Narudzbe { get; init; } = [];
    /// <summary>Zbir dodataka iz Špajza (pakerska lista sabira samo sadržaj korpica).</summary>
    public IReadOnlyList<PakovanjeStavkaDto> ZbirDodataka { get; init; } = [];
}

public record PakovanjeNarudzbaDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Kupac { get; init; } = string.Empty;
    public string? Telefon { get; init; }
    public string Adresa { get; init; } = string.Empty;
    public string? Zona { get; init; }
    /// <summary>0 = nedjelja … 6 = subota, kao DeliverySlot.</summary>
    public int? DanIsporuke { get; init; }
    public string? Termin { get; init; }
    public string? Napomena { get; init; }
    public bool Pretplatna { get; init; }
    public decimal Ukupno { get; init; }
    public IReadOnlyList<PakovanjeKorpicaDto> Korpice { get; init; } = [];
    public IReadOnlyList<PakovanjeStavkaDto> Dodaci { get; init; } = [];
}

public record PakovanjeKorpicaDto
{
    public string Naziv { get; init; } = string.Empty;
    public decimal Komada { get; init; }
    public IReadOnlyList<PakovanjeStavkaDto> Sadrzaj { get; init; } = [];
}

public record PakovanjeStavkaDto
{
    public string Naziv { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = string.Empty;
    public bool DodaoKupac { get; init; }
}

/// <summary>`GET /admin/kupci`.</summary>
public record AdminKupacDto
{
    public Guid Id { get; init; }
    public string Ime { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Telefon { get; init; }
    public bool EmailPotvrdjen { get; init; }
    public DateTime Registrovan { get; init; }
    public int BrojNarudzbi { get; init; }
    /// <summary>Zbir isporučenih narudžbi u KM.</summary>
    public decimal Isporuceno { get; init; }
    public DateTime? ZadnjaNarudzba { get; init; }
    public int AktivnePretplate { get; init; }
    public int Poeni { get; init; }
}

public record AdminKupacDetaljDto
{
    public Guid Id { get; init; }
    public string Ime { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Telefon { get; init; }
    public bool EmailPotvrdjen { get; init; }
    public DateTime Registrovan { get; init; }
    public int Poeni { get; init; }
    public IReadOnlyList<string> Adrese { get; init; } = [];
    public IReadOnlyList<AdminKupacNarudzbaDto> Narudzbe { get; init; } = [];
    public IReadOnlyList<AdminKupacPretplataDto> Pretplate { get; init; } = [];
}

public record AdminKupacNarudzbaDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Ukupno { get; init; }
    public DateTime Kreirana { get; init; }
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public bool Pretplatna { get; init; }
}

public record AdminKupacPretplataDto
{
    public Guid Id { get; init; }
    public string Korpica { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Plan { get; init; } = string.Empty;
    public int BrojIsporuka { get; init; }
}

/// <summary>`GET /sadrzaj-sajta` — null polje znači „sajt koristi ugrađeni tekst".</summary>
public record SadrzajSajtaDto
{
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Adresa { get; init; }
    public string? Podrucje { get; init; }
    public IReadOnlyList<PitanjeDto>? Pitanja { get; init; }
    public DateTime? Izmijenjeno { get; init; }
}

public record PitanjeDto
{
    public string Pitanje { get; init; } = string.Empty;
    public string Odgovor { get; init; } = string.Empty;
}
