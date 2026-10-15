using EPiljara.Application.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Jedino mjesto gdje mejl i Viber prelaze na pismo kupca (P7). Šabloni se pišu i održavaju
/// samo latinicom; ćirilica nastaje preslovljavanjem gotovog izlaza.
/// </summary>
public static class PismoPoruke
{
    public static (string Naslov, string Html) Primijeni(Pismo pismo, string naslov, string html) =>
        pismo == Pismo.Cirilica
            ? (Cirilica.UCirilicu(naslov), Cirilica.UCirilicuHtml(html))
            : (naslov, html);

    public static string Tekst(Pismo pismo, string tekst) =>
        pismo == Pismo.Cirilica ? Cirilica.UCirilicu(tekst) : tekst;
}
