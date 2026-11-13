using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public enum VrstaFiskalnogRacuna
{
    Promet,
    Refundacija,
}

public enum StatusFiskalnogRacuna
{
    /// <summary>Upisan, čeka slanje dobavljaču (ili ponovni pokušaj).</summary>
    NaCekanju,
    Izdat,
    /// <summary>Dobavljač je odbio ili nije odgovorio; <see cref="FiskalniRacun.SljedeciPokusaj"/> kaže kad opet.</summary>
    Greska,
}

/// <summary>
/// Fiskalni račun narudžbe. Nastaje NaCekanju u istom upisu kao isporuka (ili refundacija), a posao
/// ga predaje certifikovanom dobavljaču (ESIR). Stavke su snimak u trenutku nastanka (JSON).
/// Spec: docs/superpowers/specs/2026-11-04-fiskalizacija-design.md.
/// </summary>
public class FiskalniRacun : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public VrstaFiskalnogRacuna Vrsta { get; set; }
    public StatusFiskalnogRacuna Status { get; set; } = StatusFiskalnogRacuna.NaCekanju;

    public decimal Iznos { get; set; }
    public string NacinPlacanja { get; set; } = "Gotovina";
    /// <summary>Snimak stavki (lista FiskalnaStavka) u JSON-u.</summary>
    public string StavkeJson { get; set; } = "[]";

    /// <summary>Za refundaciju: prometni račun koji se poništava.</summary>
    public Guid? RefundiraniRacunId { get; set; }
    /// <summary>Za prometni: poništen refundacijom (admin vratio novac, ili vozač ispravio isporuku u
    /// neuspjelu). Narudžba smije imati samo jedan NErefundiran prometni račun; ponovna isporuka
    /// poslije ispravke dobija novi.</summary>
    public bool Refundiran { get; set; }

    public DateTime Kreiran { get; set; } = DateTime.UtcNow;

    // ----- odgovor dobavljača -----
    public string? Dobavljac { get; set; }
    public string? Broj { get; set; }
    public string? Brojac { get; set; }
    public DateTime? Izdat { get; set; }
    public string? QrSadrzaj { get; set; }
    public string? VerifikacijaUrl { get; set; }

    // ----- ponovni pokušaji -----
    public int Pokusaja { get; set; }
    public string? ZadnjaGreska { get; set; }
    /// <summary>Kad posao smije opet pokušati; null = odmah.</summary>
    public DateTime? SljedeciPokusaj { get; set; }
    /// <summary>Dobavljač je rekao da zahtjev nije ispravan — ne ponavlja se sam, čeka admina.</summary>
    public bool TrajnaGreska { get; set; }
}
