using EPiljara.Application.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Infrastructure.Persistence;

public static partial class DbSeeder
{
    /// <summary>Artikal sezonske ponude: proizvođač po imenu, kategorija po slugu, mjeseci po docs/sezonski-kalendar.md.</summary>
    private sealed record SezonskiArtikal(
        string Naziv, string Kategorija, string Proizvodjac, decimal Cijena, Unit Jedinica, decimal Zaliha,
        int[] Mjeseci, string Boja, string Opis, int Prioritet = 0);

    private static readonly int[] CijeleGodine = [];

    // Demo nabavna je 60% prodajne: korpica daje 15% više vrijednosti od cijene i čuva 25% marže,
    // pa prosječan odnos nabavna/prodajna u kutiji smije biti najviše 0,75 / 1,15 ≈ 0,65.
    private const decimal DemoUdioNabavne = 0.6m;

    /// <summary>
    /// Sezonska ponuda za punjenje korpica (2026-10-21): uz osnovni katalog, svaka veličina
    /// korpice povrća i voća ima dovoljno artikala u svakom mjesecu (Velika traži 9).
    /// Zimi voće nose jabuke iz hladnjače, orašasto i suho voće; prioritet 1 = dopuna, ide
    /// u kutiju tek iza svježih artikala. Idempotentno po slugu, kao recepti — dodaje samo
    /// ono čega nema, i na već seedovanoj demo bazi.
    /// </summary>
    private static readonly SezonskiArtikal[] SezonskaPonuda =
    [
        // ----- Povrće: zimsko i skladišno (Kovačević, Han Kola) -----
        new("Cvekla", "povrce", "Porodica Kovačević", 2.0m, Unit.Kg, 80, [1, 2, 3, 6, 7, 8, 9, 10, 11, 12], "#C98BA4",
            "Slatka crvena cvekla, zimi iz trapa. Za salatu, sok ili pečenje."),
        new("Praziluk", "povrce", "Porodica Kovačević", 3.0m, Unit.Kg, 60, [1, 2, 3, 8, 9, 10, 11, 12], "#CFE3A8",
            "Dugo bijelo stablo, podnosi mraz — vadi se iz bašte i u januaru."),
        new("Bijeli luk", "povrce", "Porodica Kovačević", 12.0m, Unit.Kg, 30, CijeleGodine, "#EFE6D2",
            "Domaći ozimi bijeli luk, ljutkast i krupnih čenova. Mladi u maju i junu, poslije sušen.", Prioritet: 1),
        new("Celer korijen", "povrce", "Porodica Kovačević", 3.0m, Unit.Kg, 40, [1, 2, 9, 10, 11, 12], "#DCD9B4",
            "Krupan korijen celera za supe i čorbe, čuva se u pijesku do februara."),
        new("Peršun", "povrce", "Porodica Kovačević", 1.5m, Unit.Kom, 80, CijeleGodine, "#A9D49A",
            "Vezica peršuna — list od proljeća do jeseni, zimi korijen iz trapa."),
        new("Pastrnak", "povrce", "Porodica Kovačević", 3.5m, Unit.Kg, 30, [1, 2, 3, 10, 11, 12], "#EDE1BE",
            "Slatkast bijeli korijen, najbolji poslije prvog mraza."),
        new("Kelj", "povrce", "Porodica Kovačević", 2.0m, Unit.Kg, 50, [1, 2, 3, 10, 11, 12], "#B5D59A",
            "Kovrdžav kelj, mraz ga učini slađim. Za variva i sarmu."),
        new("Bundeva", "povrce", "Porodica Kovačević", 4.0m, Unit.Kom, 40, [1, 2, 9, 10, 11, 12], "#F4B46C",
            "Bundeva oko 2 kg, slatkog narandžastog mesa — za pitu, čorbu i pečenje."),
        new("Grah tetovac", "povrce", "Porodica Kovačević", 7.0m, Unit.Kg, 60, CijeleGodine, "#EAD9B8",
            "Krupan suhi grah iz prošle berbe, brzo se kuha i ne raspada.", Prioritet: 1),
        new("Kiseli kupus", "povrce", "Porodica Kovačević", 2.5m, Unit.Kg, 80, [1, 2, 3, 11, 12], "#E2E5B5",
            "Glavice kiseljene u bačvi od oktobra, spremne za sarmu od novembra."),
        new("Sremuš", "povrce", "Porodica Kovačević", 2.0m, Unit.Kom, 30, [3, 4], "#9CCB8E",
            "Vezica divljeg luka iz bukove šume ispod Manjače, samo u proljeće."),
        new("Kopriva", "povrce", "Porodica Kovačević", 1.5m, Unit.Kom, 30, [3, 4, 5], "#A6CF95",
            "Vezica mladih vrhova koprive za čorbu i pitu."),

        // ----- Povrće: proljetno i ljetno (Petković, Lijevče polje) -----
        new("Mladi luk", "povrce", "Porodica Petković", 1.0m, Unit.Kom, 100, [3, 4, 5, 6], "#BEDD9C",
            "Vezica mladog luka iz plastenika, pa sa polja."),
        new("Zelena salata", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 100, [3, 4, 5, 6, 7, 8, 9, 10, 11], "#C4E3A0",
            "Glavica puterice, ubrana jutro pred dostavu."),
        new("Spanać", "povrce", "Porodica Petković", 4.0m, Unit.Kg, 40, [3, 4, 5, 9, 10, 11], "#93C487",
            "Mladi listovi spanaća, proljećna i jesenja berba."),
        new("Rotkvica", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 60, [4, 5, 6, 9, 10], "#F0A3A8",
            "Vezica hrskavih crvenih rotkvica."),
        new("Blitva", "povrce", "Porodica Petković", 3.0m, Unit.Kg, 50, [5, 6, 7, 8, 9, 10], "#A8D18F",
            "Blitva sa mesnatim drškama, bere se list po list cijelo ljeto."),
        new("Grašak", "povrce", "Porodica Petković", 4.0m, Unit.Kg, 40, [5, 6], "#B4DB93",
            "Mladi grašak u mahuni, sladak i za jelo sirov."),
        new("Keleraba", "povrce", "Porodica Petković", 1.0m, Unit.Kom, 60, [5, 6, 9, 10], "#D2E6B0",
            "Nježna keleraba veličine šake, ne odrveni."),
        new("Tikvica", "povrce", "Porodica Petković", 2.5m, Unit.Kg, 80, [5, 6, 7, 8, 9], "#B9D99A",
            "Mlade tikvice od 20-ak cm, za pohovanje i musaku."),
        new("Mahune", "povrce", "Porodica Petković", 4.0m, Unit.Kg, 50, [6, 7, 8, 9], "#AFD48E",
            "Žuta i zelena boranija bez konca."),
        new("Karfiol", "povrce", "Porodica Petković", 3.0m, Unit.Kom, 40, [6, 7, 9, 10, 11], "#F0EAD0",
            "Bijela čvrsta glavica karfiola, rana i jesenja berba."),
        new("Brokula", "povrce", "Porodica Petković", 5.0m, Unit.Kg, 30, [6, 7, 9, 10, 11], "#9DC88C",
            "Zbijena tamnozelena brokula, bere se prije nego procvjeta."),
        new("Patlidžan", "povrce", "Porodica Petković", 3.0m, Unit.Kg, 40, [7, 8, 9], "#B79AC8",
            "Sjajan tamni patlidžan za ajvar, musaku i roštilj."),
        new("Ljuta paprika", "povrce", "Porodica Petković", 5.0m, Unit.Kg, 25, [7, 8, 9, 10], "#F08F7E",
            "Duga ljuta feferona, za turšiju i uz grah."),
        new("Kukuruz šećerac", "povrce", "Porodica Petković", 1.0m, Unit.Kom, 100, [7, 8, 9], "#F5DB86",
            "Klip šećerca za kuhanje i pečenje, ubran isti dan."),

        // ----- Povrće iz sezonskog kalendara (2026-10-29): kulture koje do sada nismo imali.
        // Mjeseci su po kalendaru (vidi KalendarPovrca); cijene su okvirne, admin ih provjerava. -----
        new("Crveni krompir", "povrce", "Porodica Kovačević", 1.4m, Unit.Kg, 120, SvakiMjesec, "#E9B0B4",
            "Krompir crvene kore i žutog mesa, drži oblik u salati i pečenju."),
        new("Mladi krompirići", "povrce", "Porodica Kovačević", 2.5m, Unit.Kg, 60, [5, 6, 10, 11], "#F3DDB0",
            "Sitni mladi krompir tanke kore — pere se i peče cio."),
        new("Crveni luk", "povrce", "Porodica Kovačević", 1.8m, Unit.Kg, 80, [1, 4, 5, 6, 7, 8, 9, 10, 11, 12], "#C98AA6",
            "Blag crveni luk za salatu i roštilj."),
        new("Ljubičasti luk", "povrce", "Porodica Kovačević", 2.5m, Unit.Kg, 30, [10], "#B691C2",
            "Izduženi ljubičasti luk, sladak i sočan."),
        new("Mladi bijeli luk", "povrce", "Porodica Kovačević", 1.5m, Unit.Kom, 40, [4], "#E6EDCB",
            "Vezica mladog bijelog luka sa perima, blagog mirisa."),
        new("Mladi crni luk", "povrce", "Porodica Petković", 1.2m, Unit.Kom, 60, [6, 7, 8, 9], "#E8D3A2",
            "Mlade glavice crnog luka sa zelenim perom."),
        new("Bundeva hokaido", "povrce", "Porodica Kovačević", 3.5m, Unit.Kom, 40, [10, 11], "#F5A15C",
            "Mala narandžasta bundeva jestive kore, za čorbu i pečenje."),
        new("Batat", "povrce", "Porodica Petković", 5.0m, Unit.Kg, 40, [1, 2, 3, 4, 5, 10, 11, 12], "#F2A67E",
            "Slatki krompir narandžastog mesa, za pečenje i pire."),
        new("Čičoka", "povrce", "Porodica Kovačević", 4.0m, Unit.Kg, 30, [1, 2, 3, 4, 5, 12], "#E3CCA8",
            "Gomolj orašastog ukusa, jede se sirov ili pečen."),
        new("Celer", "povrce", "Porodica Kovačević", 1.5m, Unit.Kom, 40, [1, 2, 4, 5, 7, 8], "#CDE3A8",
            "Celer u stabljici, hrskav, za salatu i sokove."),
        new("Peršun list", "povrce", "Porodica Kovačević", 1.0m, Unit.Kom, 40, [3], "#9DD08C",
            "Vezica lisnatog peršuna bez korijena."),
        new("Crna rotkva", "povrce", "Porodica Kovačević", 2.5m, Unit.Kg, 40, [1, 2, 3, 4, 5, 12], "#8C8C8C",
            "Zimska crna rotkva oštrog ukusa, za salatu i med."),
        new("Dajkon rotkva", "povrce", "Porodica Kovačević", 2.5m, Unit.Kg, 30, [12], "#ECEBDF",
            "Duga bijela rotkva blagog ukusa."),
        new("Crveni kupus", "povrce", "Porodica Kovačević", 2.0m, Unit.Kg, 60, [1, 2, 3, 10, 11, 12], "#9E6FA8",
            "Čvrsta glavica crvenog kupusa za salatu."),
        new("Mladi kupus", "povrce", "Porodica Petković", 1.8m, Unit.Kg, 60, [1, 2, 3, 5, 6, 10, 11], "#CFE6A6",
            "Mekana glavica mladog kupusa, za salatu i dinstanje."),
        new("Kineski kupus", "povrce", "Porodica Petković", 2.0m, Unit.Kom, 30, [1, 2], "#DCEBB4",
            "Izdužena glavica nježnih listova, za salatu i wok."),
        new("Prokelj", "povrce", "Porodica Kovačević", 6.0m, Unit.Kg, 25, [12], "#9CC48A",
            "Sitne glavice prokelja, najslađe poslije mraza."),
        new("Pak čoj", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 30, [2, 11], "#B6DB9C",
            "Mlada glavica pak čoja, za brzo prženje."),
        new("Rukola", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 50, [4, 5, 6, 11, 12], "#8FC27E",
            "Vezica rukole pikantnog ukusa."),
        new("Salata kristalka", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 60, [2, 3, 4, 6, 7, 11, 12], "#CBE8A4",
            "Hrskava glavica kristalke."),
        new("Crvena salata", "povrce", "Porodica Petković", 1.5m, Unit.Kom, 50, [1, 2, 3, 4, 6, 12], "#C9879A",
            "Glavica salate crvenih kovrdžavih listova."),
        new("Špargla", "povrce", "Porodica Petković", 12.0m, Unit.Kg, 20, [4, 5], "#A9C98A",
            "Zelena špargla, rana proljetna berba."),
        new("Paprika ajvarka", "povrce", "Porodica Petković", 2.0m, Unit.Kg, 120, [10, 11, 12], "#D9534F",
            "Mesnata crvena paprika za ajvar i pečenje."),
        new("Žuta šilja", "povrce", "Porodica Petković", 3.0m, Unit.Kg, 50, [6, 7, 8, 9, 10, 11, 12], "#E9D46A",
            "Duguljasta žuta paprika tankog mesa, za punjenje i salatu."),
        new("Zelena šilja", "povrce", "Porodica Petković", 3.0m, Unit.Kg, 30, [7], "#9FCB6E",
            "Zelena paprika šilja za turšiju i prženje."),
        new("Paprika roga", "povrce", "Porodica Petković", 3.5m, Unit.Kg, 40, [6, 7, 8, 9, 11, 12], "#E0614F",
            "Duga crvena paprika roga, slatka, za pečenje."),
        new("Žuta babura", "povrce", "Porodica Petković", 3.5m, Unit.Kg, 40, [7, 8, 9], "#F2CF55",
            "Žuta babura debelog mesa."),
        new("Čeri paradajz", "povrce", "Porodica Petković", 6.0m, Unit.Kg, 30, [6, 7, 8, 9], "#E8645A",
            "Sitan slatki paradajz u grozdovima."),
        new("Kornišon", "povrce", "Porodica Petković", 3.0m, Unit.Kg, 40, [6, 7, 8, 9], "#A9CF7E",
            "Mali krastavac za salatu i kiseljenje."),
        new("Šarena boranija", "povrce", "Porodica Petković", 4.0m, Unit.Kg, 30, [10], "#D8A6A0",
            "Boranija šarenih mahuna, za čorbu i varivo."),
        new("Boranija puterica", "povrce", "Porodica Petković", 4.0m, Unit.Kg, 30, [7, 8], "#E9D98C",
            "Žuta boranija bez konca, mekana."),
        new("Raštan", "povrce", "Porodica Kovačević", 2.0m, Unit.Kg, 30, [8, 9], "#7FAE70",
            "Lisnati kupus raštan, za varivo sa suhim mesom."),

        // ----- Voće: jabuke, kruške, šljive, orašasto i suho (Jovanović, Rekavice) -----
        new("Jabuka zlatni delišes", "voce", "Gazdinstvo Jovanović", 2.2m, Unit.Kg, 120, [1, 2, 3, 4, 9, 10, 11, 12], "#F2DC8A",
            "Slatka žuta jabuka, bere se u septembru i čuva u hladnjači do proljeća."),
        new("Jabuka kožara", "voce", "Gazdinstvo Jovanović", 2.5m, Unit.Kg, 80, [1, 2, 3, 4, 10, 11, 12], "#E6C08A",
            "Stara krajiška sorta hrapave kore, u podrumu traje do aprila i tek tada je najslađa."),
        new("Ljetna jabuka petrovača", "voce", "Gazdinstvo Jovanović", 2.0m, Unit.Kg, 60, [7, 8], "#F3E3A0",
            "Prva jabuka ljeta, kiselkasta i sočna — ne čuva se, jede se odmah."),
        new("Kruška kaluđerka", "voce", "Gazdinstvo Jovanović", 3.0m, Unit.Kg, 50, [1, 2, 10, 11, 12], "#D8D894",
            "Zimska kruška: bere se tvrda u oktobru i dozrijeva u podrumu do februara."),
        new("Rana šljiva", "voce", "Gazdinstvo Jovanović", 2.5m, Unit.Kg, 60, [7], "#C2A6D8",
            "Čačanska rana, prva šljiva sezone, slatka i lako se odvaja od koštice."),
        new("Aronija", "voce", "Gazdinstvo Jovanović", 6.0m, Unit.Kg, 30, [8, 9], "#8F7FA8",
            "Svježe bobice aronije za sok i džem, opore kad su sirove."),
        new("Kesten", "voce", "Gazdinstvo Jovanović", 6.0m, Unit.Kg, 40, [10, 11], "#B99476",
            "Pitomi kesten za pečenje, krupni plodovi iz Potkozarja."),
        new("Mušmula", "voce", "Gazdinstvo Jovanović", 4.0m, Unit.Kg, 25, [11, 12], "#C9A27E",
            "Bere se poslije mraza i odleži dok ne omekša — slatko-kisela."),
        new("Lješnik", "voce", "Gazdinstvo Jovanović", 18.0m, Unit.Kg, 30, CijeleGodine, "#D9B98E",
            "Oljušten domaći lješnik, berba u avgustu.", Prioritet: 1),
        new("Suha šljiva", "voce", "Gazdinstvo Jovanović", 10.0m, Unit.Kg, 40, CijeleGodine, "#9D86B5",
            "Požegača sušena u pušnici, bez koštice i bez šećera.", Prioritet: 1),
        new("Sušene jabuke", "voce", "Gazdinstvo Jovanović", 4.0m, Unit.Kom, 50, CijeleGodine, "#EBCB97",
            "Kesica 200 g kolutova jabuke sušenih na suncu i u sušari.", Prioritet: 1),
        new("Sušene kruške", "voce", "Gazdinstvo Jovanović", 4.0m, Unit.Kom, 40, CijeleGodine, "#E2D3A0",
            "Kesica 200 g kriški kruške, za kompot i grickanje.", Prioritet: 1),
        new("Sušena aronija", "voce", "Gazdinstvo Jovanović", 4.0m, Unit.Kom, 40, CijeleGodine, "#9A8AAE",
            "Kesica 150 g sušenih bobica aronije, za čaj i musli.", Prioritet: 1),

        // ----- Voće: jagodičasto i koštičavo (Đurić, Ivanjska) -----
        new("Jagoda", "voce", "Voćnjak Đurić", 6.0m, Unit.Kg, 40, [5, 6], "#F19A9A",
            "Domaća jagoda sa otvorenog polja, bere se zrela, ne putuje danima."),
        new("Trešnja", "voce", "Voćnjak Đurić", 5.0m, Unit.Kg, 40, [5, 6], "#E48B95",
            "Krupna tamna hrskavica, bere se sa peteljkom."),
        new("Višnja", "voce", "Voćnjak Đurić", 4.0m, Unit.Kg, 40, [6, 7], "#D7808E",
            "Oblačinska višnja, kisela taman za pitu i sok."),
        new("Kajsija", "voce", "Voćnjak Đurić", 4.0m, Unit.Kg, 40, [6, 7], "#F7BE7D",
            "Mirisna kajsija za jelo i za džem."),
        new("Ribizla", "voce", "Voćnjak Đurić", 7.0m, Unit.Kg, 20, [6, 7], "#E68A8A",
            "Crvena ribizla u grozdićima, bogata vitaminom C."),
        new("Borovnica", "voce", "Voćnjak Đurić", 12.0m, Unit.Kg, 25, [6, 7, 8], "#8E9CCB",
            "Kultivisana borovnica, krupne bobice pod mrežom protiv grada."),
        new("Kupina", "voce", "Voćnjak Đurić", 6.0m, Unit.Kg, 25, [7, 8, 9], "#8F7CA6",
            "Bestrna kupina, slatka kad potpuno pocrni."),
        new("Breskva", "voce", "Voćnjak Đurić", 3.5m, Unit.Kg, 60, [7, 8, 9], "#F6B48E",
            "Sočna žuta breskva koja se odvaja od koštice."),
        new("Lubenica", "voce", "Voćnjak Đurić", 5.0m, Unit.Kom, 40, [7, 8, 9], "#EE9C9C",
            "Lubenica oko 5 kg sa pjeskovitog zemljišta uz Vrbas."),
        new("Dinja", "voce", "Voćnjak Đurić", 4.0m, Unit.Kom, 40, [7, 8, 9], "#F4D58C",
            "Mrežasta dinja oko 1,5 kg, miriše kad je zrela."),
    ];

    private static async Task SeedSezonskaPonudaAsync(AppDbContext db, CancellationToken ct)
    {
        var postojeci = (await db.Products.IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct)).ToHashSet();
        var novi = SezonskaPonuda.Where(a => !postojeci.Contains(Slug.From(a.Naziv))).ToList();
        if (novi.Count == 0)
            return;

        var kategorije = await db.Categories.ToDictionaryAsync(k => k.Slug, ct);
        var proizvodjaci = await db.Producers.ToDictionaryAsync(p => p.Name, ct);
        Producer Proizvodjac(string ime)
        {
            if (proizvodjaci.TryGetValue(ime, out var p)) return p;
            p = ime switch
            {
                "Porodica Petković" => new Producer
                {
                    Name = ime, Village = "Nova Topola",
                    Description = "Plastenici i njive u Lijevče polju. Prvo proljećno povrće stiže od njih, a ljeti tikvice, boranija i patlidžan.",
                    Latitude = 45.0469, Longitude = 17.2603, Phone = "+387 65 222 999",
                },
                "Voćnjak Đurić" => new Producer
                {
                    Name = ime, Village = "Ivanjska",
                    Description = "Voćnjak u Potkozarju: jagode, trešnje, kajsije i borovnice pod mrežom protiv grada.",
                    Latitude = 44.8728, Longitude = 17.0803, Phone = "+387 66 444 999",
                },
                _ => throw new InvalidOperationException($"Nepoznat proizvođač sezonske ponude: {ime}"),
            };
            db.Producers.Add(p);
            proizvodjaci[ime] = p;
            return p;
        }

        foreach (var a in novi)
        {
            // Bez kategorije (izmijenjen katalog) artikal se preskače — ne izmišlja se kategorija.
            if (!kategorije.TryGetValue(a.Kategorija, out var kategorija)) continue;
            var p = Prod(a.Naziv, a.Cijena, a.Jedinica, a.Zaliha, kategorija, Proizvodjac(a.Proizvodjac), a.Opis);
            p.NabavnaCijena = Math.Round(a.Cijena * DemoUdioNabavne, 2);
            p.MozeUKorpicu = true;
            p.PrioritetUKorpici = a.Prioritet;
            p.Mjeseci = a.Mjeseci;
            p.Boja = a.Boja;
            db.Products.Add(p);
        }
        await db.SaveChangesAsync(ct);
    }
}
