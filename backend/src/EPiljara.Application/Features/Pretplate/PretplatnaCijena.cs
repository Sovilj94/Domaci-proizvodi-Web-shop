namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Jedino mjesto koje zna cjenovno pravilo pretplate. Zovu ga i upit koji puni ekran i
/// komanda koja pravi narudžbu — dvije kopije ovog izraza bi se razišle prvom izmjenom
/// praga, a razlika bi se vidjela tek na računu pouzećem.
/// </summary>
public static class PretplatnaCijena
{
    /// <summary>
    /// Cijena naredne isporuke. <paramref name="brojIsporuka"/> je broj VEĆ isporučenih
    /// narudžbi te pretplate, pa je pri pragu 5 šesta isporuka prva po nižoj cijeni.
    ///
    /// Negativan brojač se čita kao nula: brojač je izveden podatak (vidi BrojacIsporuka),
    /// a pokvaren red u bazi ne smije dati cijenu nižu od dogovorene.
    /// </summary>
    public static decimal Izracunaj(decimal punaCijena, int brojIsporuka, PretplateOpcije o)
    {
        var isporuceno = Math.Max(0, brojIsporuka);
        if (isporuceno < o.PragPopusta) return punaCijena;

        // Zaokruživanje na parno je isto kao svuda u backendu (.NET podrazumijevano);
        // frontend zaokružuje od nule, pa se za pola feninga razilaze — server je mjerodavan.
        return Math.Round(punaCijena * (1 - o.Popust), 2, MidpointRounding.ToEven);
    }

    /// <summary>Koliko još isporuka dijeli kupca od niže cijene; 0 kad je već ima.</summary>
    public static int DoPopusta(int brojIsporuka, PretplateOpcije o) =>
        Math.Max(0, o.PragPopusta - Math.Max(0, brojIsporuka));
}
