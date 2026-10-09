using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KorpicaPodobnostTests
{
    private static Product Napravi(
        string naziv,
        bool mozeUKorpicu = true,
        bool dostupan = true,
        decimal stock = 10m,
        VrstaProizvoda vrsta = VrstaProizvoda.Obican,
        int[]? mjeseci = null) => new()
    {
        Name = naziv,
        Slug = naziv.ToLowerInvariant(),
        Price = 2m,
        Unit = Unit.Kg,
        Stock = stock,
        IsAvailable = dostupan,
        MozeUKorpicu = mozeUKorpicu,
        Vrsta = vrsta,
        Mjeseci = mjeseci ?? [],
    };

    [Fact]
    public async Task UpitPodobnih_propusta_samo_obicne_dostupne_sa_zalihom()
    {
        using var t = new TestDb();
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var pr = new Producer { Name = "Marko", Village = "Manjača" };

        var dobar      = Napravi("Krompir");
        var neMoze     = Napravi("Krompir2", mozeUKorpicu: false);
        var nedostupan = Napravi("Krompir3", dostupan: false);
        var bezZalihe  = Napravi("Krompir4", stock: 0m);
        var korpica    = Napravi("Korpica", vrsta: VrstaProizvoda.Korpica);

        foreach (var p in new[] { dobar, neMoze, nedostupan, bezZalihe, korpica })
        {
            p.Category = kat;
            p.Producer = pr;
            t.Db.Products.Add(p);
        }
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var imena = await KorpicaPodobnost.UpitPodobnih(t.Db.Products.AsNoTracking())
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal(["Krompir"], imena);
    }

    [Fact]
    public void USezoni_propusta_prazan_popis_mjeseci_i_podudaran_mjesec()
    {
        var uvijek = Napravi("Jaja");                     // prazan Mjeseci = cijele godine
        var samoJul = Napravi("Paradajz", mjeseci: [7]);
        var samoDec = Napravi("Kupus", mjeseci: [12]);

        var rezultat = KorpicaPodobnost
            .USezoni([uvijek, samoJul, samoDec], 7)
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(["Jaja", "Paradajz"], rezultat);
    }
}
