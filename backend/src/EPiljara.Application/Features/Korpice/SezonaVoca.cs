using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Jedino mjesto koje zna kad je sezona voća (spec 2026-11-26, odluka 2). Mjeseci dolaze iz
/// <c>Podesavanja</c> (<see cref="KorpiceOpcije.SezonaVocaOd"/>/<see cref="KorpiceOpcije.SezonaVocaDo"/>);
/// oba prazna znače voće cijele godine. Mjerodavan je mjesec ISPORUKE, ne današnji dan — kupac 30. aprila
/// naručuje za maj.
/// </summary>
public static class SezonaVoca
{
    private static readonly string[] GenitivMjeseca =
        ["januara", "februara", "marta", "aprila", "maja", "juna", "jula", "avgusta", "septembra", "oktobra", "novembra", "decembra"];

    private static readonly string[] LokativMjeseca =
        ["januaru", "februaru", "martu", "aprilu", "maju", "junu", "julu", "avgustu", "septembru", "oktobru", "novembru", "decembru"];

    /// <summary>Da li je mjesec (1–12) u sezoni voća. Raspon je uključiv i smije preko nove godine (11–3).</summary>
    public static bool UTijeku(KorpiceOpcije o, int mjesec)
    {
        if (o.SezonaVocaOd is not int od || o.SezonaVocaDo is not int @do) return true;
        return od <= @do ? mjesec >= od && mjesec <= @do : mjesec >= od || mjesec <= @do;
    }

    /// <summary>Samo voćna korpica može biti van sezone.</summary>
    public static bool VanSezone(KorpiceOpcije o, TipKorpice? tip, int mjesec) =>
        tip == TipKorpice.Voce && !UTijeku(o, mjesec);

    /// <summary>Mjesec dana isporuke te sedmice, po lokalnom vremenu (Sarajevo).</summary>
    public static int MjesecIsporuke(int godina, int sedmica, DayOfWeek dan, string vrijemeOd) =>
        Lokalno(SedmicaHelper.DatumIsporuke(godina, sedmica, dan, vrijemeOd)).Month;

    /// <summary>Godina u kojoj sezona sljedeći put počinje, gledano od isporuke van sezone.</summary>
    public static int GodinaPovratka(KorpiceOpcije o, DateTime isporukaUtc)
    {
        var lok = Lokalno(isporukaUtc);
        if (o.SezonaVocaOd is not int od) return lok.Year;
        return lok.Month <= od ? lok.Year : lok.Year + 1;
    }

    /// <summary>„od maja", „do oktobra".</summary>
    public static string Genitiv(int mjesec) => GenitivMjeseca[mjesec - 1];

    /// <summary>„u maju".</summary>
    public static string Lokativ(int mjesec) => LokativMjeseca[mjesec - 1];

    private static DateTime Lokalno(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SedmicaHelper.Zona);
}
