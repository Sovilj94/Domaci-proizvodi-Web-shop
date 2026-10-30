namespace EPiljara.Application.Features.Catalog;

/// <summary>Podešavanja prodavnice — puni ih red <c>Podesavanja</c> iz baze (vidi PodesavanjaMapa).</summary>
public class ProdavnicaOpcije
{
    /// <summary>Od ove zalihe naniže artikal je „pri kraju" („Još samo X na stanju").</summary>
    public decimal PragPriKraju { get; set; }
}
