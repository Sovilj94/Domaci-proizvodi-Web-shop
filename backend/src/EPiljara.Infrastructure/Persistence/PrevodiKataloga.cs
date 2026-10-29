using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>
/// Početni engleski nazivi kataloga (jezik sajta English, 2026-10-29). Na startu se upisuju samo
/// u prazna polja (<c>NazivEn</c> null), pa ono što admin unese ostaje. Proizvod bez prevoda se
/// na engleskom sajtu prikazuje srpskim nazivom.
/// </summary>
public static class PrevodiKataloga
{
    private static readonly Dictionary<string, string> Kategorije = new()
    {
        ["povrce"] = "Vegetables",
        ["voce"] = "Fruit",
        ["mlijecni-proizvodi"] = "Dairy",
        ["meso"] = "Meat and cured meats",
        ["jaja"] = "Eggs",
        ["med"] = "Honey and bee products",
        ["zimnica"] = "Preserves and jams",
    };

    private static readonly Dictionary<string, string> Proizvodi = new()
    {
        ["aronija"] = "Aronia berries",
        ["babura-paprika"] = "Bell pepper",
        ["bagremov-med"] = "Acacia honey",
        ["batat"] = "Sweet potato",
        ["bijeli-luk"] = "Garlic",
        ["bijeli-sir-u-rasoli"] = "White cheese in brine",
        ["blitva"] = "Swiss chard",
        ["boranija-puterica"] = "Yellow wax beans",
        ["borovnica"] = "Blueberries",
        ["breskva"] = "Peaches",
        ["brokula"] = "Broccoli",
        ["bundeva"] = "Butternut squash",
        ["bundeva-hokaido"] = "Hokkaido pumpkin",
        ["celer"] = "Celery",
        ["celer-korijen"] = "Celeriac",
        ["crna-rotkva"] = "Black radish",
        ["crni-luk"] = "Onions",
        ["crvena-salata"] = "Red lettuce",
        ["crveni-krompir"] = "Red potatoes",
        ["crveni-kupus"] = "Red cabbage",
        ["crveni-luk"] = "Red onions",
        ["cvekla"] = "Beetroot",
        ["dajkon-rotkva"] = "Daikon radish",
        ["dinja"] = "Melon",
        ["domaca-kobasica"] = "Homemade sausage",
        ["domaca-pavlaka"] = "Homemade sour cream",
        ["domaci-jogurt"] = "Homemade yoghurt",
        ["domaci-krompir"] = "White potatoes",
        ["dunja"] = "Quince",
        ["dzem-od-sljive"] = "Plum jam",
        ["grah-tetovac"] = "Tetovac beans",
        ["grasak"] = "Peas",
        ["grozdje"] = "Grapes",
        ["jabuka-idared"] = "Idared apples",
        ["jabuka-kozara"] = "Kožara apples",
        ["jabuka-zlatni-delises"] = "Golden Delicious apples",
        ["jagoda"] = "Strawberries",
        ["jaja-iz-slobodnog-uzgoja"] = "Free-range eggs",
        ["kajmak"] = "Kajmak (clotted cream)",
        ["kajsija"] = "Apricots",
        ["karfiol"] = "Cauliflower",
        ["keleraba"] = "Kohlrabi",
        ["kelj"] = "Savoy cabbage",
        ["kesten"] = "Chestnuts",
        ["kineski-kupus"] = "Napa cabbage",
        ["kiseli-kupus"] = "Sauerkraut",
        ["kopriva"] = "Nettle",
        ["kornison"] = "Gherkins",
        ["krastavac"] = "Cucumbers",
        ["kruska"] = "Pears",
        ["kruska-kaludjerka"] = "Kaluđerka pears",
        ["kukuruz-secerac"] = "Sweet corn",
        ["kupina"] = "Blackberries",
        ["kupus"] = "Cabbage",
        ["livadski-med"] = "Meadow honey",
        ["ljetna-jabuka-petrovaca"] = "Petrovača summer apples",
        ["ljesnik"] = "Hazelnuts",
        ["ljubicasti-luk"] = "Shallots",
        ["ljuta-paprika"] = "Hot peppers",
        ["lubenica"] = "Watermelon",
        ["mahune"] = "Green beans",
        ["malina"] = "Raspberries",
        ["mladi-bijeli-luk"] = "Green garlic",
        ["mladi-crni-luk"] = "Young onions",
        ["mladi-kravlji-sir"] = "Fresh cow's cheese",
        ["mladi-krompirici"] = "Baby potatoes",
        ["mladi-kupus"] = "Spring cabbage",
        ["mladi-luk"] = "Spring onions",
        ["mrkva"] = "Carrots",
        ["musmula"] = "Medlars",
        ["orah"] = "Walnuts",
        ["pak-coj"] = "Pak choi",
        ["paprika-ajvarka"] = "Ajvar peppers",
        ["paprika-roga"] = "Pointed red peppers",
        ["paradajz-iz-plastenika"] = "Greenhouse tomatoes",
        ["pastrnak"] = "Parsnips",
        ["patlidzan"] = "Aubergine",
        ["persun"] = "Parsley",
        ["persun-list"] = "Flat-leaf parsley",
        ["pecenica"] = "Smoked pork loin",
        ["praziluk"] = "Leeks",
        ["prokelj"] = "Brussels sprouts",
        ["prsut"] = "Prosciutto",
        ["rana-sljiva"] = "Early plums",
        ["rastan"] = "Collard greens",
        ["ribizla"] = "Redcurrants",
        ["rotkvica"] = "Radishes",
        ["rukola"] = "Rocket",
        ["salata-kristalka"] = "Iceberg lettuce",
        ["skorup"] = "Skorup (aged cream)",
        ["spanac"] = "Spinach",
        ["sremus"] = "Wild garlic",
        ["sudzuk"] = "Sudžuk (dry sausage)",
        ["suha-slanina"] = "Smoked bacon",
        ["suha-sljiva"] = "Prunes",
        ["suho-meso"] = "Smoked dried beef",
        ["susena-aronija"] = "Dried aronia berries",
        ["susene-jabuke"] = "Dried apples",
        ["susene-kruske"] = "Dried pears",
        ["svjeze-kravlje-mlijeko"] = "Fresh cow's milk",
        ["tikvica"] = "Courgettes",
        ["tresnja"] = "Sweet cherries",
        ["visnja"] = "Sour cherries",
        ["zelena-salata"] = "Butterhead lettuce",
        ["zelena-silja"] = "Green pointed peppers",
        ["ceri-paradajz"] = "Cherry tomatoes",
        ["cicoka"] = "Jerusalem artichoke",
        ["cvarci"] = "Pork cracklings",
        ["sarena-boranija"] = "Borlotti beans",
        ["sljiva-pozegaca"] = "Požegača plums",
        ["spargla"] = "Asparagus",
        ["zuta-babura"] = "Yellow bell pepper",
        ["zuta-silja"] = "Yellow pointed peppers",
        ["domaci-ajvar"] = "Homemade ajvar",
        ["mala-korpica-povrca"] = "Small vegetable box",
        ["srednja-korpica-povrca"] = "Medium vegetable box",
        ["velika-korpica-povrca"] = "Large vegetable box",
        ["mala-korpica-voca"] = "Small fruit box",
        ["srednja-korpica-voca"] = "Medium fruit box",
        ["velika-korpica-voca"] = "Large fruit box",
    };

    /// <returns>Broj upisanih prevoda.</returns>
    public static async Task<int> PopuniAsync(AppDbContext db, CancellationToken ct = default)
    {
        var upisano = 0;
        foreach (var k in await db.Categories.Where(k => k.NazivEn == null).ToListAsync(ct))
            if (Kategorije.TryGetValue(k.Slug, out var en)) { k.NazivEn = en; upisano++; }
        foreach (var p in await db.Products.Where(p => p.NazivEn == null).ToListAsync(ct))
            if (Proizvodi.TryGetValue(p.Slug, out var en)) { p.NazivEn = en; upisano++; }
        await db.SaveChangesAsync(ct);
        return upisano;
    }
}
