namespace EPiljara.Application.Features.Pretplate;

/// <summary>Podešavanja pretplata, iz sekcije „Pretplate" u konfiguraciji.</summary>
public class PretplateOpcije
{
    public const string Sekcija = "Pretplate";

    /// <summary>
    /// Koliko isporuka kupac mora imati iza sebe da bi platio nižu cijenu. Prag je broj
    /// VEĆ ISPORUČENIH, pa pri pragu 5 šesta isporuka postaje prva jeftinija — popust se
    /// zarađuje unazad, što je svjesno odstupanje od uzora (farmit naplati period unaprijed
    /// karticom i zato smije dati popust odmah; ovdje se plaća pouzećem, pa je duži period
    /// samo obećanje).
    /// </summary>
    public int PragPopusta { get; set; } = 5;

    /// <summary>Udio popusta na punu cijenu korpice, 0.10 = 10%.</summary>
    public decimal Popust { get; set; } = 0.10m;

    /// <summary>
    /// Koliko sati prije roka naručivanja pozadinski posao pravi narudžbu za tu sedmicu.
    /// Ranije od roka namjerno: kupac koji se predomisli mora imati prozor u kojem može
    /// pauzirati ili otkazati prije nego što narudžba uopšte nastane.
    /// </summary>
    public int SatiUnaprijed { get; set; } = 48;

    /// <summary>Najduža pauza koju kupac može zadati odjednom, u sedmicama.</summary>
    public int MaksPauzaSedmica { get; set; } = 8;
}
