using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Koliko je čega još slobodno za jednu sedmicu isporuke — jedino mjesto koje to zna.
///
/// <see cref="Product.Stock"/> artikla iz Špajza se skida pri narudžbi, ali sadržaj korpice
/// NE: kupac ga mijenja sve do roka, pa bi svako skidanje i vraćanje zalihe bilo mjesto za
/// grešku. Umjesto toga se obećano računa iz samih redova sadržaja narudžbi te sedmice
/// (<see cref="StavkaKorpiceNarudzbe"/> × broj korpica, sve osim otkazanih — isti skup koji
/// sabira pakerska lista). Slobodno = stanje − obećano u korpicama.
///
/// Zato korpica nema svoju zalihu: koliko ih još može da se proda izvodi se iz sadržaja
/// sedmice i slobodnog stanja artikala (<see cref="Kapacitet"/>). Sve veličine troše iste
/// artikle, pa svaka prodana korpica smanjuje kapacitet i ostalih.
/// </summary>
public sealed class Zalihe
{
    private readonly Dictionary<Guid, decimal> _obecano;

    // Ono što je ova ista operacija već uzela, a još nije upisano (narudžba sa više stavki).
    private readonly Dictionary<Guid, decimal> _uzeto = new();

    private Zalihe(Dictionary<Guid, decimal> obecano) => _obecano = obecano;

    /// <param name="osimOrderItema">
    /// Korpica koju kupac upravo mijenja: njeni dosadašnji redovi se ne broje, jer ih nova
    /// verzija zamjenjuje.
    /// </param>
    public static async Task<Zalihe> UcitajAsync(
        IAppDbContext db, int godina, int sedmica, CancellationToken ct, Guid? osimOrderItema = null)
    {
        // Množenje i zbir u memoriji: SQLite (testovi) ne računa decimal u SQL-u.
        var redovi = await db.StavkeKorpiceNarudzbe
            .Where(s => s.OrderItem!.Order!.Godina == godina
                        && s.OrderItem.Order.Sedmica == sedmica
                        && s.OrderItem.Order.Status != OrderStatus.Cancelled
                        && (osimOrderItema == null || s.OrderItemId != osimOrderItema))
            .Select(s => new { s.ProductId, s.Kolicina, s.OrderItem!.Quantity })
            .ToListAsync(ct);

        return new Zalihe(redovi
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Kolicina * r.Quantity)));
    }

    /// <summary>Učitava zalihe za sedmicu za koju se trenutno naručuje.</summary>
    public static async Task<(Zalihe Zalihe, int Godina, int Sedmica)> ZaSedmicuProdajeAsync(
        IAppDbContext db, KorpiceOpcije opcije, CancellationToken ct)
    {
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije, ct);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);
        return (await UcitajAsync(db, godina, sedmica, ct), godina, sedmica);
    }

    /// <summary>Obećano u korpicama (uključujući ono što je ova operacija već uzela).</summary>
    public decimal Obecano(Guid productId) =>
        _obecano.GetValueOrDefault(productId) + _uzeto.GetValueOrDefault(productId);

    /// <summary>
    /// Stanje minus obećano u korpicama. Gleda samo stanje, ne i to je li artikal u katalogu:
    /// artikal skinut ili obrisan poslije objave sadržaja je i dalje fizički tu i ide u kutiju
    /// (vidi SnapshotKorpice). Ko smije da ga NOVO izabere, odlučuje KorpicaPodobnost.
    /// </summary>
    public decimal Slobodno(Product p) => Math.Max(0m, p.Stock - Obecano(p.Id));

    /// <summary>
    /// Koliko još korpica sa ovim sadržajem može da se napravi i koji artikal prvi ponestane.
    /// Važi kao da se prodaje samo ova korpica — sve dijele iste artikle.
    /// </summary>
    public (int Moguce, string? UskoGrlo) Kapacitet(
        SedmicniSadrzaj sadrzaj, IReadOnlyDictionary<Guid, Product> artikli)
    {
        int? najmanje = null;
        string? usko = null;
        foreach (var st in sadrzaj.Stavke.Where(s => s.Kolicina > 0))
        {
            var moguce = artikli.TryGetValue(st.ProductId, out var p)
                ? (int)Math.Floor(Slobodno(p) / st.Kolicina)
                : 0;
            if (najmanje is null || moguce < najmanje)
            {
                najmanje = moguce;
                usko = p?.Name;
            }
        }
        return (najmanje ?? 0, usko);
    }

    /// <summary>
    /// Kapacitet svake korpice za sedmicu prodaje. Korpica bez objavljenog sadržaja te
    /// sedmice nije u mapi — ne može se napuniti, pa se ni ne prodaje.
    /// </summary>
    public static async Task<Dictionary<Guid, (int Moguce, string? UskoGrlo)>> KapacitetiAsync(
        IAppDbContext db, KorpiceOpcije opcije, CancellationToken ct)
    {
        var (zalihe, godina, sedmica) = await ZaSedmicuProdajeAsync(db, opcije, ct);
        var sadrzaji = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .Where(s => s.Godina == godina && s.Sedmica == sedmica && s.Status == StatusSadrzaja.Objavljen)
            .ToListAsync(ct);
        var idjevi = sadrzaji.SelectMany(s => s.Stavke).Select(s => s.ProductId).Distinct().ToList();
        var artikli = await db.Products.IgnoreQueryFilters().Where(p => idjevi.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return sadrzaji.ToDictionary(s => s.KorpicaId, s => zalihe.Kapacitet(s, artikli));
    }

    /// <summary>
    /// Artikal iz Špajza: baca <c>nema_zalihe</c> kad traženog nema slobodnog. Samo provjera —
    /// <see cref="Product.Stock"/> skida pozivalac, kao i do sada.
    /// </summary>
    public void ProvjeriArtikal(Product p, decimal kolicina)
    {
        var slobodno = Slobodno(p);
        if (kolicina > slobodno)
            throw new DomainException(
                slobodno <= 0
                    ? $"{p.Name} je rasprodat."
                    : $"Od artikla {p.Name} ostalo je još {slobodno:0.##} {p.Unit.ToApi()}.",
                "nema_zalihe");
    }

    /// <summary>
    /// Sadržaj korpice na novoj narudžbi (redovi × broj korpica): baca <c>korpica_rasprodata</c>
    /// kad nekog artikla nema dovoljno, inače ga uzima, da ga sljedeća stavka iste narudžbe vidi.
    /// </summary>
    public async Task UzmiKorpicuAsync(IAppDbContext db, OrderItem stavka, Product korpica, CancellationToken ct)
    {
        var idjevi = stavka.KorpicaStavke.Select(r => r.ProductId).ToList();
        var artikli = await db.Products.IgnoreQueryFilters().Where(p => idjevi.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var r in stavka.KorpicaStavke)
        {
            var slobodno = artikli.TryGetValue(r.ProductId, out var p) ? Slobodno(p) : 0m;
            if (r.Kolicina * stavka.Quantity > slobodno)
                throw new DomainException(
                    $"{korpica.Name} je rasprodata za ovu sedmicu — nema dovoljno: {r.ProductName}.",
                    "korpica_rasprodata");
        }
        foreach (var r in stavka.KorpicaStavke) Uzmi(r.ProductId, r.Kolicina * stavka.Quantity);
    }

    public void Uzmi(Guid productId, decimal kolicina) =>
        _uzeto[productId] = _uzeto.GetValueOrDefault(productId) + kolicina;
}
