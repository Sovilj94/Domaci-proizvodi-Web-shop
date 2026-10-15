namespace EPiljara.Domain.Enums;

/// <summary>
/// Pismo kojim kupac čita sajt, mejlove i Viber. Jezik je jedan (srpski); ćirilica se
/// dobija preslovljavanjem latinice (<c>Cirilica</c> u Application/Common). Čuva se kao tekst.
/// </summary>
public enum Pismo
{
    Latinica,
    Cirilica,
}
