namespace EPiljara.Application.DTOs;

/// <summary>Analitika jednog mjeseca (po datumu isporuke). Samo admin.</summary>
public record PoslovanjeMjesecaDto
{
    public int Godina { get; init; }
    public int Mjesec { get; init; }
    public PrihodDto Prihod { get; init; } = new();
    /// <summary>Isti brojevi za prethodni mjesec — za ▲/▼ na karticama.</summary>
    public PrihodDto Prethodni { get; init; } = new();
    public ProdajaDto Prodaja { get; init; } = new();
    public KupciDto Kupci { get; init; } = new();
    public PretplateAnalitikaDto Pretplate { get; init; } = new();
    public DostavaAnalitikaDto Dostava { get; init; } = new();
    public IReadOnlyList<GazdinstvoDto> Gazdinstva { get; init; } = [];
}

public record PrihodDto
{
    public decimal PrometRobe { get; init; }
    public decimal Dostava { get; init; }
    public decimal PromoPopust { get; init; }
    public decimal PoeniPopust { get; init; }
    public decimal Neto { get; init; }
    public decimal Nabavno { get; init; }
    public decimal Zarada { get; init; }
    /// <summary>Zarada / promet robe (0–1). Null kad nema prometa ili nijedna stavka nema nabavnu.</summary>
    public decimal? Marza { get; init; }
    public int BrojNarudzbi { get; init; }
    public decimal ProsjecnaNarudzba { get; init; }
    public decimal UdioKorpica { get; init; }
    public decimal UdioSpajza { get; init; }
    /// <summary>Bar jedna stavka nema snimak nabavne pa je računata po današnjoj cijeni.</summary>
    public bool Procjena { get; init; }
    /// <summary>Stavke bez ikakve nabavne cijene — nisu u trošku.</summary>
    public int BezNabavneCijene { get; init; }
}

public record ProdajaDto
{
    public IReadOnlyList<KorpicaProdajeDto> Korpice { get; init; } = [];
    public IReadOnlyList<KategorijaProdajeDto> Spajz { get; init; } = [];
    public IReadOnlyList<ArtikalProdajeDto> TopArtikli { get; init; } = [];
    public IReadOnlyList<ArtikalProdajeDto> NajslabijiArtikli { get; init; } = [];
}

public record KorpicaProdajeDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Tip { get; init; } = string.Empty;
    public string Velicina { get; init; } = string.Empty;
    public decimal Komada { get; init; }
    public decimal Prihod { get; init; }
    public decimal Nabavno { get; init; }
    public decimal? Marza { get; init; }
}

public record KategorijaProdajeDto
{
    public string Kategorija { get; init; } = string.Empty;
    public decimal Prihod { get; init; }
    public decimal Nabavno { get; init; }
    public decimal? Marza { get; init; }
}

/// <summary>Artikal sabran iz Špajza i sadržaja korpica; prihod sadržaja po cijeni iz korpice.</summary>
public record ArtikalProdajeDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public decimal Prihod { get; init; }
    public decimal Zarada { get; init; }
}

public record KupciDto
{
    public int Aktivni { get; init; }
    public int AktivniPrethodni { get; init; }
    public int Novi { get; init; }
    public int Povratni { get; init; }
    /// <summary>Dio kupaca aktivnih prošlog mjeseca koji su aktivni i ovog (0–1). Null bez prošlog.</summary>
    public decimal? Zadrzavanje { get; init; }
    public decimal PotrosnjaPoKupcu { get; init; }
    public decimal NarudzbiPoKupcu { get; init; }
    public int Gosti { get; init; }
    public int Registrovani { get; init; }
    public int NoviPrekoPreporuke { get; init; }
    public IReadOnlyList<KupacAnalitikeDto> TopKupci { get; init; } = [];
}

public record KupacAnalitikeDto
{
    public string Ime { get; init; } = string.Empty;
    public bool Gost { get; init; }
    public int Narudzbi { get; init; }
    public decimal Neto { get; init; }
}

public record PretplateAnalitikaDto
{
    public int AktivneNaKraju { get; init; }
    public int Nove { get; init; }
    public int Otkazane { get; init; }
    public decimal? StopaOtkazivanja { get; init; }
    /// <summary>Samo za tekući mjesec — istorija pauza se ne čuva.</summary>
    public int? Pauzirane { get; init; }
    public IReadOnlyList<PlanAnalitikeDto> PoPlanu { get; init; } = [];
    public decimal UdioPrihoda { get; init; }
}

public record PlanAnalitikeDto
{
    public string Plan { get; init; } = string.Empty;
    public int Aktivne { get; init; }
}

public record DostavaAnalitikaDto
{
    public IReadOnlyList<ZonaAnalitikeDto> PoZoni { get; init; } = [];
    public int Isporucene { get; init; }
    public int Neuspjele { get; init; }
    public decimal? Uspjesnost { get; init; }
    public int Otkazane { get; init; }
    public decimal OtkazaneVrijednost { get; init; }
    public decimal? OtkazaneUdio { get; init; }
    public decimal? ProsjecnaOcjena { get; init; }
    public int BrojOcjena { get; init; }
}

public record ZonaAnalitikeDto
{
    public string Zona { get; init; } = string.Empty;
    public int Narudzbi { get; init; }
    public decimal Neto { get; init; }
}

public record GazdinstvoDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    public decimal Nabavno { get; init; }
    public IReadOnlyList<ArtikalGazdinstvaDto> Artikli { get; init; } = [];
}

public record ArtikalGazdinstvaDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public decimal Nabavno { get; init; }
}

/// <summary>Godišnji pregled: 12 mjeseci i prihod po kategoriji po mjesecima.</summary>
public record PoslovanjeGodineDto
{
    public int Godina { get; init; }
    public IReadOnlyList<MjesecGodineDto> Mjeseci { get; init; } = [];
    public IReadOnlyList<SezonaKategorijeDto> Sezona { get; init; } = [];
}

public record MjesecGodineDto
{
    public int Mjesec { get; init; }
    public decimal Neto { get; init; }
    public decimal Zarada { get; init; }
    public decimal? Marza { get; init; }
    public int Narudzbi { get; init; }
    public int AktivniKupci { get; init; }
    /// <summary>Aktivne na kraju mjeseca; null za mjesec koji još nije počeo.</summary>
    public int? AktivnePretplate { get; init; }
    public bool Procjena { get; init; }
}

public record SezonaKategorijeDto
{
    public string Kategorija { get; init; } = string.Empty;
    /// <summary>Prihod po mjesecima, indeks 0 = januar.</summary>
    public IReadOnlyList<decimal> Mjeseci { get; init; } = [];
}
