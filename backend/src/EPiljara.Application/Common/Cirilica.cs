using System.Text;
using System.Text.RegularExpressions;

namespace EPiljara.Application.Common;

/// <summary>
/// Preslovljavanje srpske latinice u ćirilicu — za mejlove i Viber kupcu koji je izabrao
/// ćirilicu. Ista pravila žive u <c>frontend/src/lib/pismo/preslovi.ts</c>; ugovor između
/// njih su vektori u <c>docs/pismo-vektori.json</c> koje čitaju testovi oba projekta.
/// Izmjena pravila ide u oba fajla i u vektore, nikad samo u jedan.
/// </summary>
public static partial class Cirilica
{
    private static readonly Dictionary<char, string> Slova = new()
    {
        ['a'] = "а", ['b'] = "б", ['c'] = "ц", ['č'] = "ч", ['ć'] = "ћ", ['d'] = "д", ['đ'] = "ђ",
        ['e'] = "е", ['f'] = "ф", ['g'] = "г", ['h'] = "х", ['i'] = "и", ['j'] = "ј", ['k'] = "к",
        ['l'] = "л", ['m'] = "м", ['n'] = "н", ['o'] = "о", ['p'] = "п", ['r'] = "р", ['s'] = "с",
        ['š'] = "ш", ['t'] = "т", ['u'] = "у", ['v'] = "в", ['z'] = "з", ['ž'] = "ж",
    };

    private static readonly Dictionary<string, string> Digrafi = new()
    {
        ["lj"] = "љ", ["nj"] = "њ", ["dž"] = "џ",
    };

    /// <summary>Granica prefiksa, ne jedno slovo: nad-živjeti, pod-župan, kon-jugacija, in-jekcija.</summary>
    private static readonly (string Prefiks, int Rastav)[] Izuzeci =
    [
        ("nadž", 2), ("podž", 2), ("konjug", 2), ("injek", 1),
    ];

    private static readonly string[] Brendovi = ["Google", "Facebook", "Viber", "OpenStreetMap", "OSM", "Leaflet"];

    private const string Naziv = "E piljara";

    [GeneratedRegex(@"[\p{L}\p{N}@._:/+-]+|[^\p{L}\p{N}@._:/+-]+")]
    private static partial Regex Token();

    [GeneratedRegex(@"^[A-ZČĆŠŽĐ0-9-]+$")]
    private static partial Regex KodOblik();

    [GeneratedRegex(@"(<!--.*?-->|<style.*?</style>|<[^>]+>|&[#\w]+;)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HtmlNeTekst();

    /// <summary>Latinica → ćirilica. Idempotentno: tekst već na ćirilici izlazi isti.</summary>
    public static string UCirilicu(string tekst)
    {
        if (string.IsNullOrEmpty(tekst)) return tekst;
        return string.Join(Naziv, tekst.Split(Naziv).Select(Dio));
    }

    /// <summary>
    /// Preslovljava samo tekst između tagova. Tagovi (sa atributima — <c>href</c>, <c>style</c>),
    /// komentari, <c>&lt;style&gt;</c> blokovi i entiteti ostaju netaknuti.
    /// </summary>
    public static string UCirilicuHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var sb = new StringBuilder(html.Length);
        var pos = 0;
        foreach (Match m in HtmlNeTekst().Matches(html))
        {
            sb.Append(UCirilicu(html[pos..m.Index]));
            sb.Append(m.Value);
            pos = m.Index + m.Length;
        }
        sb.Append(UCirilicu(html[pos..]));
        return sb.ToString();
    }

    private static string Dio(string tekst) =>
        Token().Replace(tekst, m => ImaSlovo(m.Value) && !OstajeLatinica(m.Value) ? Rijec(m.Value) : m.Value);

    private static bool ImaSlovo(string s) => s.Any(char.IsLetter);

    private static bool OstajeLatinica(string token)
    {
        // Tačka ili dvotačka na kraju rečenice ulazi u token, pa se pravila provjeravaju nad
        // jezgrom: „EP-2610-0059." je i dalje kod.
        var rijec = token.TrimEnd('.', '_', ':', '/', '+', '-');
        if (rijec.IndexOfAny(['q', 'w', 'x', 'y', 'Q', 'W', 'X', 'Y']) >= 0) return true;
        if (rijec.Contains('@') || rijec.Contains("://") || rijec.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return true;
        if (KodOblik().IsMatch(rijec) && rijec.Any(c => char.IsDigit(c) || c == '-')) return true;
        return Brendovi.Any(b => rijec.StartsWith(b, StringComparison.Ordinal));
    }

    private static string Slovo(char lat, string cir) =>
        char.IsUpper(lat) ? cir.ToUpperInvariant() : cir;

    private static string Rijec(string rijec)
    {
        var mala = rijec.ToLowerInvariant();
        var izuzetak = Izuzeci.FirstOrDefault(i => mala.StartsWith(i.Prefiks, StringComparison.Ordinal));
        var rastav = izuzetak.Prefiks is null ? -1 : izuzetak.Rastav;
        var sb = new StringBuilder(rijec.Length);
        for (var i = 0; i < rijec.Length; i++)
        {
            if (i + 1 < mala.Length && i != rastav && Digrafi.TryGetValue(mala.Substring(i, 2), out var dig))
            {
                sb.Append(Slovo(rijec[i], dig));
                i++;
                continue;
            }
            sb.Append(Slova.TryGetValue(mala[i], out var c) ? Slovo(rijec[i], c) : rijec[i].ToString());
        }
        return sb.ToString();
    }
}
