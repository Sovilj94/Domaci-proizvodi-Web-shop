namespace EPiljara.Application.Features.Poeni;

/// <summary>Podešavanja programa poena, iz sekcije „Poeni" u konfiguraciji.</summary>
public class PoeniOpcije
{
    public const string Sekcija = "Poeni";

    /// <summary>Koliko poena kupac zaradi za svaku cijelu KM plaćene robe.</summary>
    public int PoenaPoKM { get; set; } = 1;

    /// <summary>Vrijednost jednog poena pri trošenju, u KM (20 poena = 1 KM).</summary>
    public decimal VrijednostPoenaKM { get; set; } = 0.05m;

    /// <summary>Koliko poena dobiju i preporučilac i novi kupac kad ovaj prvi put dobije isporuku.</summary>
    public int NagradaPreporuka { get; set; } = 100;
}
