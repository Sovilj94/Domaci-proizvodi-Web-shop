using System.Globalization;
using System.Text;
using EPiljara.Application.Common.Interfaces;

namespace EPiljara.Application.Features.Geocoding;

/// <summary>Područje dostave — puni ga red <c>Podesavanja</c> iz baze (vidi PodesavanjaMapa).</summary>
public class DostavaOpcije
{
    /// <summary>Grad dostave kako ga piše geokoder („Banja Luka"); prazno = bez ograničenja.</summary>
    public string GradDostave { get; set; } = string.Empty;
}

/// <summary>
/// Da li pogodak geokodera pada u grad dostave. Gleda i grad i opštinu, jer selo na teritoriji
/// grada (npr. Lazarevo) kod Photona ima svoj „city", a opština mu je „Grad Banja Luka".
/// </summary>
public static class PodrucjeDostave
{
    /// <summary>
    /// <c>true</c>/<c>false</c> kad geokoder zna grad ili opštinu; <c>null</c> kad ne zna ni jedno
    /// (tada odluku ne donosimo — pin bez adrese i dalje može u narudžbu, kao i ranije).
    /// Bez podešenog grada dostave sve je u području.
    /// </summary>
    public static bool? Pripada(GeocodeHit hit, string gradDostave)
    {
        var cilj = Normalizuj(gradDostave);
        if (cilj.Length == 0) return true;

        var kandidati = new[] { hit.Grad, hit.Opstina }
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => Normalizuj(k!))
            .ToList();
        if (kandidati.Count == 0) return null;
        return kandidati.Contains(cilj);
    }

    private static readonly string[] Prefiksi = ["grad ", "opstina ", "opcina "];

    /// <summary>„Grad Бања Лука" → „banja luka": mala slova, latinica, bez kvačica i prefiksa.</summary>
    public static string Normalizuj(string s)
    {
        var t = Latinica(s.Trim().ToLowerInvariant());
        var bez = new StringBuilder(t.Length);
        foreach (var c in t.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                bez.Append(c);
        t = bez.ToString().Replace('đ', 'd').Normalize(NormalizationForm.FormC);
        foreach (var p in Prefiksi)
            if (t.StartsWith(p, StringComparison.Ordinal)) t = t[p.Length..];
        return t.Trim();
    }

    private static string Latinica(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch
            {
                'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d", 'ђ' => "đ", 'е' => "e",
                'ж' => "ž", 'з' => "z", 'и' => "i", 'ј' => "j", 'к' => "k", 'л' => "l", 'љ' => "lj",
                'м' => "m", 'н' => "n", 'њ' => "nj", 'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s",
                'т' => "t", 'ћ' => "ć", 'у' => "u", 'ф' => "f", 'х' => "h", 'ц' => "c", 'ч' => "č",
                'џ' => "dž", 'ш' => "š",
                _ => c.ToString(),
            });
        return sb.ToString();
    }
}
