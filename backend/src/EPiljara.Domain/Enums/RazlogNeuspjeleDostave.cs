namespace EPiljara.Domain.Enums;

/// <summary>
/// Zašto vozač nije predao narudžbu. Bira ga u aplikaciji kad označi neuspjelu dostavu; admin po
/// njemu zna koga treba zvati, a kome ispraviti adresu. Narudžbe od prije ovog polja i one koje
/// je zatvorila starija verzija aplikacije nemaju razlog (null).
/// </summary>
public enum RazlogNeuspjeleDostave
{
    NijeKodKuce,
    NeJavljaSe,
    PogresnaAdresa,
    OdbioPrijem,
    Ostalo,
}
