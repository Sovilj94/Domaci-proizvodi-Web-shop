namespace EPiljara.Application.Common;

/// <summary>
/// Današnji datum po vremenu u Sarajevu. Rok promo koda („do 31.10.") je datum koji je admin
/// mislio po našem kalendaru, ne po UTC-u servera — u ponoć po UTC-u je kod Sarajeva već 1 ili
/// 2 sata sljedećeg dana.
/// </summary>
public static class LokalniDatum
{
    private static readonly TimeZoneInfo Zona = Nadji();

    private static TimeZoneInfo Nadji()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }

    public static DateOnly Danas() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona));
}
