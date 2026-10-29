using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

public static partial class DbSeeder
{
    private static readonly int[] SvakiMjesec = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

    /// <summary>
    /// Kalendar povrća za korpice (2026-10-29), po sezonskom kalendaru sa farmit.rs: u kojim
    /// mjesecima kultura ide u korpicu. Naše kulture koje tamo nemaju parnjaka (grašak, kiseli
    /// kupus, kopriva, sremuš) ne idu u korpicu — ostaju u Špajzu. Ista lista je u migraciji
    /// <c>SezonaPovrcaKalendar</c>, koja jednom ažurira postojeće baze; ovdje važi za novu bazu.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, int[]> KalendarPovrca = new Dictionary<string, int[]>
    {
        ["babura-paprika"] = [7, 8, 9, 10],
        ["bijeli-luk"] = [1, 2, 3, 4, 6, 7, 9, 10, 11, 12],
        ["blitva"] = [1, 2, 3, 4, 10, 11, 12],
        ["brokula"] = [11, 12],
        ["bundeva"] = [1, 3, 9, 10, 11, 12],
        ["celer-korijen"] = [1, 2, 4, 5, 6, 7, 8],
        ["crni-luk"] = SvakiMjesec,
        ["cvekla"] = [1, 2, 3, 4, 8, 9, 10, 11, 12],
        ["domaci-krompir"] = SvakiMjesec,
        ["grah-tetovac"] = [1, 2, 3, 10, 11, 12],
        ["karfiol"] = [11, 12],
        ["keleraba"] = [2, 3, 4],
        ["kelj"] = [1, 2, 3, 12],
        ["krastavac"] = [5, 6, 7, 8, 9, 10],
        ["kukuruz-secerac"] = [8, 9, 10],
        ["kupus"] = [1, 2, 3, 5, 6, 10, 11, 12],
        ["ljuta-paprika"] = [6, 7, 8, 9, 10],
        ["mahune"] = [7, 8, 10],
        ["mladi-luk"] = [1, 2, 3, 4, 5, 6, 11, 12],
        ["mrkva"] = SvakiMjesec,
        ["paradajz-iz-plastenika"] = [6, 7, 8, 9],
        ["pastrnak"] = [1, 2, 3, 4, 5, 6, 8, 10, 11, 12],
        ["patlidzan"] = [8, 9, 10],
        ["persun"] = [1, 2, 3, 4, 5, 6, 8, 10, 11, 12],
        ["praziluk"] = [1, 2, 3, 4, 5, 9, 10, 11, 12],
        ["rotkvica"] = [1, 2, 3, 4, 5, 10, 11, 12],
        ["spanac"] = [2, 3, 4, 5, 10],
        ["tikvica"] = [4, 5, 6, 7, 8, 9, 10],
        ["zelena-salata"] = [1, 2, 3, 4, 5, 6, 7, 10, 11, 12],
    };

    /// <summary>Povrće koje nije u kalendaru ne ide u korpicu.</summary>
    internal static readonly string[] PovrceVanKorpice = ["grasak", "kiseli-kupus", "kopriva", "sremus"];

    /// <summary>Primijeni kalendar na tek seedovanu bazu (postojeće ažurira migracija).</summary>
    private static async Task PrimijeniKalendarPovrcaAsync(AppDbContext db, CancellationToken ct)
    {
        var slugovi = KalendarPovrca.Keys.Concat(PovrceVanKorpice).ToList();
        var proizvodi = await db.Products.Where(p => slugovi.Contains(p.Slug)).ToListAsync(ct);
        foreach (var p in proizvodi)
        {
            if (KalendarPovrca.TryGetValue(p.Slug, out var mjeseci))
            {
                p.Mjeseci = mjeseci;
                p.MozeUKorpicu = true;
            }
            else
            {
                p.MozeUKorpicu = false;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
