namespace EPiljara.Application.Common;

/// <summary>
/// Boja pločice namirnice (početna, sadržaj korpice, analize) — po uzoru na farmit, gdje
/// svaka kultura ima svoju boju. Admin je može zadati na proizvodu; bez nje se uzima
/// pastelna boja iz palete, izvedena iz naziva, pa je ista namirnica uvijek iste boje i
/// nijedna pločica nije bezbojna. Jedino mjesto gdje se boja određuje.
/// </summary>
public static class BojaProizvoda
{
    public static readonly IReadOnlyList<string> Paleta =
    [
        "#F4C27A", "#E6B06A", "#E8A3A8", "#F5A97F", "#B9D89A",
        "#A8D5BA", "#C8B6E2", "#F7D58B", "#9FCBE0", "#E9B7CE",
    ];

    public static string Za(string? zadana, string naziv)
    {
        if (!string.IsNullOrWhiteSpace(zadana)) return zadana.Trim().ToUpperInvariant();
        var hes = 0;
        foreach (var c in naziv.Trim().ToLowerInvariant()) hes = unchecked(hes * 31 + c);
        return Paleta[(int)((uint)hes % (uint)Paleta.Count)];
    }

    public static bool JeValidna(string? boja) =>
        boja is null || System.Text.RegularExpressions.Regex.IsMatch(boja, "^#[0-9A-Fa-f]{6}$");
}
