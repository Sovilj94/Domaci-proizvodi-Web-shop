using EPiljara.Application.Features.Slike;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>
/// Upisuje početne slike iz <c>SeedSlike/</c> (uz API) u tabelu <c>Slike</c>. Dodaje samo ključeve
/// kojih u bazi nema, pa ne dira ništa što je admin promijenio. Radi na svakom startu, nezavisno
/// od SEED_DATA, jer postojeći <c>ImageUrl</c>-ovi (i nakon migracije) pokazuju na <c>/slike/...</c>.
/// </summary>
public static class SlikeUvoz
{
    public static string PodrazumijevaniFolder => Path.Combine(AppContext.BaseDirectory, "SeedSlike");

    /// <returns>Broj upisanih slika.</returns>
    public static async Task<int> UveziAsync(AppDbContext db, string? folder = null, CancellationToken ct = default)
    {
        folder ??= PodrazumijevaniFolder;
        if (!Directory.Exists(folder)) return 0;

        var postojeci = (await db.Slike.Select(s => s.Kljuc).ToListAsync(ct)).ToHashSet();
        var upisano = 0;

        foreach (var put in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order())
        {
            var tip = SlikePravila.TipZaEkstenziju(Path.GetExtension(put));
            if (tip is null) continue;
            var kljuc = Path.GetRelativePath(folder, put).Replace('\\', '/');
            if (postojeci.Contains(kljuc)) continue;

            var podaci = await File.ReadAllBytesAsync(put, ct);
            db.Slike.Add(new Slika { Kljuc = kljuc, ContentType = tip, Podaci = podaci, Velicina = podaci.Length });
            upisano++;

            // Po desetak slika u jednom snimanju — ~16 MB odjednom je nepotrebno velik zahtjev.
            if (upisano % 10 == 0)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
            }
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return upisano;
    }

    /// <summary>
    /// Proizvodu bez izreza dodijeli <c>izrezi/{slug}.png</c> ako ta slika postoji u bazi — tako
    /// početni izrezi (SeedSlike/izrezi) stignu i na postojeće proizvode. Izrez koji je admin
    /// postavio ili obrisao (prazan string) se ne dira; dira se samo null.
    /// </summary>
    /// <returns>Broj povezanih proizvoda.</returns>
    public static async Task<int> PoveziIzrezeAsync(AppDbContext db, CancellationToken ct = default)
    {
        var izrezi = (await db.Slike
                .Where(s => s.Kljuc.StartsWith("izrezi/"))
                .Select(s => s.Kljuc)
                .ToListAsync(ct))
            .ToHashSet();
        if (izrezi.Count == 0) return 0;

        var bez = await db.Products.Where(p => p.IzrezUrl == null).ToListAsync(ct);
        var povezano = 0;
        foreach (var p in bez)
        {
            var kljuc = $"izrezi/{p.Slug}.png";
            if (!izrezi.Contains(kljuc)) continue;
            p.IzrezUrl = SlikePravila.Url(kljuc);
            povezano++;
        }
        await db.SaveChangesAsync(ct);
        return povezano;
    }
}
