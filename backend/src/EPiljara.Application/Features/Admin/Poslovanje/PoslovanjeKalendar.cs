using System.Globalization;
using EPiljara.Application.Features.Korpice;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>
/// Narudžba pripada mjesecu u kojem je ISPORUČENA: sedmica se prevodi u datum dana isporuke.
/// Sedmica 40/2026 (pon 28.9) sa isporukom četvrtkom je oktobar, ne septembar.
/// </summary>
public static class PoslovanjeKalendar
{
    public static (int Godina, int Mjesec) MjesecIsporuke(int godina, int sedmica, DayOfWeek dan)
    {
        var d = ISOWeek.ToDateTime(godina, sedmica, dan);
        return (d.Year, d.Month);
    }

    /// <summary>ISO sedmice čiji dan isporuke pada u dati mjesec, redom.</summary>
    public static List<(int Godina, int Sedmica)> SedmiceMjeseca(int godina, int mjesec, DayOfWeek dan)
    {
        var d = new DateTime(godina, mjesec, 1);
        while (d.DayOfWeek != dan) d = d.AddDays(1);
        var sedmice = new List<(int, int)>();
        for (; d.Month == mjesec; d = d.AddDays(7))
            sedmice.Add((ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d)));
        return sedmice;
    }

    public static (int Godina, int Mjesec) Prethodni(int godina, int mjesec) =>
        mjesec == 1 ? (godina - 1, 12) : (godina, mjesec - 1);

    public static int Indeks(int godina, int mjesec) => godina * 12 + mjesec - 1;

    /// <summary>Ponoć prvog dana mjeseca po lokalnom (Sarajevo) vremenu, izražena u UTC.</summary>
    public static DateTime PocetakUtc(int godina, int mjesec) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(godina, mjesec, 1, 0, 0, 0, DateTimeKind.Unspecified), SedmicaHelper.Zona);
}
