namespace EPiljara.Application.Features.Poeni;

/// <summary>Podešavanja programa poena — puni ih red <c>Podesavanja</c> iz baze (vidi PodesavanjaMapa).</summary>
public class PoeniOpcije
{
    /// <summary>Koliko poena kupac zaradi za svaku cijelu KM plaćene robe.</summary>
    public int PoenaPoKM { get; set; }

    /// <summary>Vrijednost jednog poena pri trošenju, u KM (20 poena = 1 KM).</summary>
    public decimal VrijednostPoenaKM { get; set; }

    /// <summary>Koliko poena dobiju i preporučilac i novi kupac kad ovaj prvi put dobije isporuku.</summary>
    public int NagradaPreporuka { get; set; }
}
