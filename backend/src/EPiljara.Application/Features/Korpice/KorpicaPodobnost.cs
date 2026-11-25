using EPiljara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Jedan pojam „artikal smije u korpicu", koji dijele generator sedmičnog sadržaja i kupčev
/// editor. Dvije kopije ovog uslova se raziđu prvog dana kad se jedna promijeni, a
/// razilaženje se primijeti tek kad kupac doda artikal koji generator nikad ne bi izabrao.
///
/// Podijeljen je na dva dijela jer <see cref="Product.Mjeseci"/> je int[] u C# a CSV u bazi:
/// sezona se NE MOŽE filtrirati u SQL-u i mora u memoriju poslije učitavanja.
/// </summary>
public static class KorpicaPodobnost
{
    /// <summary>Dio uslova koji ide u SQL. Sezona se dodaje odvojeno sa <see cref="USezoni"/>.</summary>
    public static IQueryable<Product> UpitPodobnih(IQueryable<Product> izvor) =>
        izvor.Where(p => p.Vrsta == VrstaProizvoda.Obican
                         && p.MozeUKorpicu
                         && p.IsAvailable
                         && p.Stock > 0);

    /// <summary>
    /// Sezonski filter, u memoriji. Prazan <see cref="Product.Mjeseci"/> znači „cijele godine".
    /// </summary>
    public static IEnumerable<Product> USezoni(IEnumerable<Product> ucitani, int mjesec) =>
        ucitani.Where(p => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(mjesec));

    /// <summary>
    /// Kategorije vrste ove korpice (sezonski kalendar korpica). Prazno = korpica bez vrste ili
    /// vrsta bez kategorija — tada nema ograničenja, da pogrešno podešena korpica ne ostane bez ponude.
    /// </summary>
    public static async Task<HashSet<Guid>> KategorijeAsync(IAppDbContext db, Product korpica, CancellationToken ct) =>
        korpica.TipKorpice is null
            ? []
            : (await db.KorpicaKategorije
                .Where(k => k.TipKorpice == korpica.TipKorpice)
                .Select(k => k.CategoryId)
                .ToListAsync(ct)).ToHashSet();

    /// <summary>
    /// Šta kupac smije staviti u SVOJU korpicu: artikli iz kategorija njene vrste (povrtna →
    /// povrće iz kalendara), plus predložene zamjene koje je admin odredio za tu sedmicu.
    /// Kupac više ne bira iz cijele ponude prodavnice (izmjena odluke P2b).
    /// </summary>
    public static IEnumerable<Product> ZaKorpicu(
        IEnumerable<Product> ponuda, IReadOnlySet<Guid> kategorije, IReadOnlyCollection<Guid> preporucene) =>
        kategorije.Count == 0
            ? ponuda
            : ponuda.Where(p => kategorije.Contains(p.CategoryId) || preporucene.Contains(p.Id));
}
