namespace EPiljara.Application.Features.Pretplate;

/// <summary>Podešavanja pretplata, iz sekcije „Pretplate" u konfiguraciji.</summary>
public class PretplateOpcije
{
    public const string Sekcija = "Pretplate";

    /// <summary>
    /// Popust plana na punu cijenu korpice, 0.10 = 10%. Važi od prve isporuke (R2, po uzoru na
    /// farmit) — zamijenio je P3 pravilo „popust od šeste isporuke".
    /// </summary>
    public decimal PopustNedjeljna { get; set; } = 0.10m;
    public decimal PopustMjesecna { get; set; } = 0.15m;
    public decimal PopustTriMjeseca { get; set; } = 0.20m;

    /// <summary>
    /// Na koliko isporuka se kupac obavezuje. Dok obaveza traje, otkaz sa naloga nije ponuđen
    /// (pauza jeste) — otkazuje admin. Pouzećem se obaveza ne može naplatiti; prihvaćen rizik.
    /// </summary>
    public int ObavezaMjesecna { get; set; } = 4;
    public int ObavezaTriMjeseca { get; set; } = 13;

    /// <summary>
    /// Koliko sati prije roka naručivanja pozadinski posao pravi narudžbu za tu sedmicu.
    /// Ranije od roka namjerno: kupac koji se predomisli mora imati prozor u kojem može
    /// pauzirati ili otkazati prije nego što narudžba uopšte nastane.
    /// </summary>
    public int SatiUnaprijed { get; set; } = 48;

    /// <summary>
    /// Koliko sati prije roka admin dobija mejl ako neka korpica sa aktivnim pretplatama još nema
    /// objavljen sadržaj (A2b). Bez sadržaja posao preskače sedmicu, a kupci dobijaju mejl tek poslije.
    /// </summary>
    public int AlarmSatiPrijeRoka { get; set; } = 24;

    /// <summary>Najduža pauza koju kupac može zadati odjednom, u sedmicama.</summary>
    public int MaksPauzaSedmica { get; set; } = 8;
}
