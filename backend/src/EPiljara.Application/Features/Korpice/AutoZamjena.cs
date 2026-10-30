using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Zamjena jednog artikla u kupčevoj korpici drugim, tako da vrijednost kutije ostane u
/// granicama budžeta (95–100% cilja) i da nabavni trošak ne obori maržu. Jedno mjesto za
/// sva četiri ulaza: kupčeva zamjena sa izabranim artiklom, „izaberi umjesto mene",
/// artikli sa kupčevog spiska „ne stavljaj mi" pri narudžbi, i vraćanje na početni sadržaj.
/// </summary>
public static class AutoZamjena
{
    public record Izbor(Product Artikal, decimal Kolicina, decimal Cijena);

    /// <summary>Razlog zbog kojeg kandidat ne može biti zamjena — za poruku kupcu kad bira sam.</summary>
    public enum Prepreka { Nema, NeStaje, NemaZalihe, Marza }

    /// <summary>
    /// Kandidati za zamjenu, redom kojim ih „izaberi umjesto mene" proba: osnovni artikli prije
    /// dopune (svježe se mijenja svježim, ne suhim grahom), a unutar toga predložene zamjene
    /// sadržaja, pa artikli iz kategorija ove korpice, pa ostala sezonska ponuda.
    /// </summary>
    public static async Task<List<Product>> KandidatiAsync(
        IAppDbContext db, SedmicniSadrzaj sadrzaj, Product korpica, int mjesec, CancellationToken ct)
    {
        var kategorije = korpica.TipKorpice is null
            ? []
            : await db.KorpicaKategorije
                .Where(k => k.TipKorpice == korpica.TipKorpice)
                .Select(k => k.CategoryId)
                .ToListAsync(ct);
        var preporucene = sadrzaj.Zamjene.Select(z => z.ProductId).ToList();

        return KorpicaPodobnost
            .USezoni(await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec)
            .OrderBy(p => p.PrioritetUKorpici)
            .ThenBy(p => preporucene.IndexOf(p.Id) is var i and >= 0 ? i : int.MaxValue)
            .ThenByDescending(p => kategorije.Contains(p.CategoryId))
            .ThenBy(p => p.Name)
            .ToList();
    }

    /// <summary>Cijena novog artikla: snimak iz sadržaja (zamjena, pa šablon), pa katalog.</summary>
    public static decimal Cijena(SedmicniSadrzaj sadrzaj, Product nov) =>
        sadrzaj.Zamjene.FirstOrDefault(z => z.ProductId == nov.Id)?.JedinicnaCijena
        ?? sadrzaj.Stavke.FirstOrDefault(s => s.ProductId == nov.Id)?.JedinicnaCijena
        ?? nov.Price;

    /// <summary>Nabavne cijene artikala koji su u kutiji (iz kataloga, i obrisanih).</summary>
    public static Task<Dictionary<Guid, decimal>> NabavneAsync(
        IAppDbContext db, IEnumerable<StavkaKorpiceNarudzbe> stavke, CancellationToken ct)
    {
        var idjevi = stavke.Select(s => s.ProductId).Distinct().ToList();
        return db.Products.IgnoreQueryFilters()
            .Where(p => idjevi.Contains(p.Id) && p.NabavnaCijena != null)
            .ToDictionaryAsync(p => p.Id, p => p.NabavnaCijena!.Value, ct);
    }

    /// <summary>
    /// Može li <paramref name="nov"/> zamijeniti <paramref name="stara"/>; ako može, u kojoj
    /// količini i po kojoj cijeni. Podobnost (sezona, dostupnost) provjerava pozivalac —
    /// kandidati dolaze iz <see cref="KandidatiAsync"/> ili su već provjereni.
    /// </summary>
    public static (Izbor? Izbor, Prepreka Prepreka) Probaj(
        Product nov,
        StavkaKorpiceNarudzbe stara,
        IReadOnlyCollection<StavkaKorpiceNarudzbe> ostale,
        SedmicniSadrzaj sadrzaj,
        decimal cijenaKorpice,
        IReadOnlyDictionary<Guid, decimal> nabavne,
        KorpiceOpcije o,
        Func<Product, decimal>? slobodno = null)
    {
        var cijena = Cijena(sadrzaj, nov);
        if (cijena <= 0) return (null, Prepreka.NeStaje);

        var cilj = sadrzaj.CiljnaVrijednost;
        var kolicina = ZamijeniArtikalUKorpiciHandler.Kolicina(
            stara.Kolicina * stara.JedinicnaCijena,
            ostale.Sum(s => s.Kolicina * s.JedinicnaCijena),
            cijena,
            Math.Round(cilj * IzmijeniMojuKorpicuHandler.DonjaGranica, 2),
            Math.Round(cilj * IzmijeniMojuKorpicuHandler.GornjaGranica, 2),
            nov.Unit);
        if (kolicina is null) return (null, Prepreka.NeStaje);
        // Slobodno po jednoj korpici (vidi Zalihe); bez njega ostaje golo stanje.
        if (kolicina > (slobodno?.Invoke(nov) ?? nov.Stock)) return (null, Prepreka.NemaZalihe);

        // Nabavni budžet: zamjena koja kutiju gura preko njega se odbija — osim kad je i
        // dalje jeftinija od stanja prije. Bez nabavne cijene novog artikla nema provjere.
        if (nov.NabavnaCijena is > 0)
        {
            decimal Trosak(StavkaKorpiceNarudzbe s) => s.Kolicina * nabavne.GetValueOrDefault(s.ProductId);
            var ostaliTrosak = ostale.Sum(Trosak);
            var prije = ostaliTrosak + Trosak(stara);
            var poslije = ostaliTrosak + kolicina.Value * nov.NabavnaCijena.Value;
            var budzet = cijenaKorpice * (1 - o.MinimalnaMarza);
            if (poslije > budzet && poslije > prije) return (null, Prepreka.Marza);
        }

        return (new Izbor(nov, kolicina.Value, cijena), Prepreka.Nema);
    }

    /// <summary>Prvi kandidat koji staje; preskače artikle koji su već u kutiji i one iz <paramref name="iskljuci"/>.</summary>
    public static Izbor? Nadji(
        IEnumerable<Product> kandidati,
        StavkaKorpiceNarudzbe stara,
        IReadOnlyCollection<StavkaKorpiceNarudzbe> ostale,
        SedmicniSadrzaj sadrzaj,
        decimal cijenaKorpice,
        IReadOnlyDictionary<Guid, decimal> nabavne,
        ISet<Guid> iskljuci,
        KorpiceOpcije o,
        Func<Product, decimal>? slobodno = null)
    {
        foreach (var p in kandidati)
        {
            if (p.Id == stara.ProductId || iskljuci.Contains(p.Id) || ostale.Any(s => s.ProductId == p.Id)) continue;
            var (izbor, _) = Probaj(p, stara, ostale, sadrzaj, cijenaKorpice, nabavne, o, slobodno);
            if (izbor is not null) return izbor;
        }
        return null;
    }

    /// <summary>Novi red narudžbe za izabranu zamjenu.</summary>
    public static StavkaKorpiceNarudzbe Red(Guid orderItemId, Izbor izbor, SedmicniSadrzaj sadrzaj) => new()
    {
        OrderItemId = orderItemId,
        ProductId = izbor.Artikal.Id,
        ProductName = izbor.Artikal.Name,
        Unit = izbor.Artikal.Unit,
        Kolicina = izbor.Kolicina,
        JedinicnaCijena = izbor.Cijena,
        DodaoKupac = sadrzaj.Stavke.All(s => s.ProductId != izbor.Artikal.Id),
    };

    /// <summary>
    /// Artikle sa kupčevog spiska „ne stavljaj mi" mijenja prvom zamjenom koja staje. Radi nad
    /// listom u memoriji (narudžba još nije upisana). Artikal za koji zamjena ne postoji
    /// ostaje — kutija se ne smije isprazniti ispod cilja.
    /// </summary>
    public static async Task PrimijeniIzbjegnuteAsync(
        IAppDbContext db,
        List<StavkaKorpiceNarudzbe> stavke,
        Guid orderItemId,
        SedmicniSadrzaj sadrzaj,
        Product korpica,
        decimal cijenaKorpice,
        Guid kupacId,
        int mjesec,
        KorpiceOpcije o,
        CancellationToken ct,
        Func<Product, decimal>? slobodno = null)
    {
        var izbjegnuti = (await db.IzbjegnutiArtikli
            .Where(x => x.UserId == kupacId)
            .Select(x => x.ProductId)
            .ToListAsync(ct)).ToHashSet();
        if (izbjegnuti.Count == 0 || stavke.All(s => !izbjegnuti.Contains(s.ProductId))) return;
        if (sadrzaj.CiljnaVrijednost <= 0) return;

        var kandidati = await KandidatiAsync(db, sadrzaj, korpica, mjesec, ct);
        var nabavne = await NabavneAsync(db, stavke, ct);
        foreach (var k in kandidati.Where(k => k.NabavnaCijena is > 0)) nabavne[k.Id] = k.NabavnaCijena!.Value;

        foreach (var stara in stavke.Where(s => izbjegnuti.Contains(s.ProductId)).ToList())
        {
            var ostale = stavke.Where(s => s != stara).ToList();
            var izbor = Nadji(kandidati, stara, ostale, sadrzaj, cijenaKorpice, nabavne, izbjegnuti, o, slobodno);
            if (izbor is null) continue;
            stavke[stavke.IndexOf(stara)] = Red(orderItemId, izbor, sadrzaj);
        }
    }
}
