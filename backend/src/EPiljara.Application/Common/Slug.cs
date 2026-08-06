using System.Text;
using System.Text.RegularExpressions;

namespace EPiljara.Application.Common;

/// <summary>Generates URL-friendly slugs, transliterating BHS diacritics.</summary>
public static partial class Slug
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['č'] = "c", ['ć'] = "c", ['đ'] = "dj", ['š'] = "s", ['ž'] = "z",
    };

    public static string From(string input)
    {
        var sb = new StringBuilder();
        foreach (var ch in input.ToLowerInvariant())
            sb.Append(Map.TryGetValue(ch, out var replacement) ? replacement : ch);

        return NonSlugChars().Replace(sb.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
