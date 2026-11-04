using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

public enum StatusEmaila
{
    NaCekanju,
    Poslato,
    Greska,
}

/// <summary>
/// Jedan mejl u redu za slanje (outbox). Nastaje odmah (gotov HTML), a šalje ga pozadinski posao — zahtjev
/// kupca nikad ne čeka mejl server, a pad dobavljača znači samo kasnije slanje, ne izgubljen mejl.
/// </summary>
public class EmailPoruka : Entity
{
    public string Primalac { get; set; } = string.Empty;
    public string? PrimalacIme { get; set; }
    public string Naslov { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    /// <summary>Kratka oznaka vrste ("potvrda_narudzbe", "plan_vozaca", "obavjestenje"…), za pregled i filtriranje.</summary>
    public string Vrsta { get; set; } = string.Empty;
    /// <summary>Jedinstven ključ kad isti događaj ne smije poslati dva mejla (npr. "odbijena:{orderId}"); null = bez provjere.</summary>
    public string? Kljuc { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? KampanjaId { get; set; }

    public StatusEmaila Status { get; set; } = StatusEmaila.NaCekanju;
    public int Pokusaja { get; set; }
    public DateTime? SljedeciPokusaj { get; set; }
    public string? ZadnjaGreska { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
    public DateTime? Poslata { get; set; }
    /// <summary>Id poruke kod dobavljača (Brevo messageId), za traženje u njihovom logu.</summary>
    public string? DobavljacId { get; set; }
    public string? Transport { get; set; }
}

/// <summary>Ručno poslato obavještenje grupi primalaca (sa stranice Mejlovi).</summary>
public class EmailKampanja : Entity
{
    public string Naslov { get; set; } = string.Empty;
    public string Tekst { get; set; } = string.Empty;
    /// <summary>"sedmica", "pretplatnici", "kupci", "novosti", "vozaci", "radnici".</summary>
    public string Grupa { get; set; } = string.Empty;
    public string GrupaOpis { get; set; } = string.Empty;
    public int BrojPrimalaca { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
    public Guid? PoslaoId { get; set; }
}
