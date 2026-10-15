namespace EPiljara.Application.Common;

/// <summary>
/// Ime za javni prikaz: „marko marković" -> „Marko M.". Izdvojeno iz Ocjena jer isto skraćivanje
/// treba i istoriji poena (ime preporučenog), a dva oblika imena na ekranu bi djelovala kao greška.
/// </summary>
internal static class ImeSkraceno
{
    /// <summary>
    /// Prvo ime i inicijal ZADNJE riječi, razmaci sažeti, najviše 60 znakova (granica Utisak.Ime);
    /// jedna riječ -> samo ta riječ; prazno -> „Kupac".
    /// </summary>
    public static string Za(string? punoIme)
    {
        var rijeci = (punoIme ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (rijeci.Length == 0) return "Kupac";
        var prva = rijeci[0];
        var ime = char.ToUpperInvariant(prva[0]) + prva[1..].ToLowerInvariant();
        if (rijeci.Length > 1)
            ime += " " + char.ToUpperInvariant(rijeci[^1][0]) + ".";
        return ime.Length > 60 ? ime[..60] : ime;
    }
}
