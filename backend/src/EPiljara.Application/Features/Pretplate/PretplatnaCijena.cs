using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Jedino mjesto koje zna cjenovno pravilo pretplate i obavezu plana. Zovu ga upit koji puni
/// ekran, javni `KorpicaDto`, admin spisak i komanda koja pravi narudžbu — dvije kopije ovog
/// izraza bi se razišle prvom izmjenom, a razlika bi se vidjela tek na računu pouzećem.
/// </summary>
public static class PretplatnaCijena
{
    /// <summary>Cijena isporuke za plan — ista od prve isporuke (R2).</summary>
    public static decimal Izracunaj(decimal punaCijena, PlanPretplate plan, PretplateOpcije o) =>
        // Zaokruživanje na parno je isto kao svuda u backendu (.NET podrazumijevano);
        // frontend zaokružuje od nule, pa se za pola feninga razilaze — server je mjerodavan.
        Math.Round(punaCijena * (1 - Popust(plan, o)), 2, MidpointRounding.ToEven);

    public static decimal Popust(PlanPretplate plan, PretplateOpcije o) => plan switch
    {
        PlanPretplate.Mjesecna => o.PopustMjesecna,
        PlanPretplate.TriMjeseca => o.PopustTriMjeseca,
        _ => o.PopustNedjeljna,
    };

    /// <summary>Na koliko isporuka se kupac obavezuje izborom plana (0 = bez obaveze).</summary>
    public static int Obaveza(PlanPretplate plan, PretplateOpcije o) => plan switch
    {
        PlanPretplate.Mjesecna => o.ObavezaMjesecna,
        PlanPretplate.TriMjeseca => o.ObavezaTriMjeseca,
        _ => 0,
    };

    /// <summary>
    /// Koliko isporuka obaveze je ostalo. Negativan brojač se čita kao nula: brojač je izveden
    /// podatak (vidi BrojacIsporuka), a pokvaren red ne smije osloboditi obaveze.
    /// </summary>
    public static int PreostaloObaveze(int obaveza, int brojIsporuka) =>
        Math.Max(0, obaveza - Math.Max(0, brojIsporuka));
}
