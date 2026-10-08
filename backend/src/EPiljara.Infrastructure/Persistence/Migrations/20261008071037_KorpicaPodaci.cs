using System;
using System.Globalization;
using EPiljara.Application.Common;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Podaci za korpice — ne šema. Migracija <c>Korpice</c> (20261007075847) je dodala
    /// kolone i tri tabele, ali ih ostavila PRAZNIM: svi stvarni redovi (6
    /// <c>KorpicaKategorije</c> veza, 15 <c>Product</c> redova tipa Korpica, i backfill
    /// <c>MozeUKorpicu</c> za postojeću robu) dolaze isključivo iz <c>DbSeeder</c>, koji
    /// staje na prvu postojeću <c>Category</c> i radi samo u Developmentu ili uz
    /// <c>Seed:Demo=true</c>. Na stvarnoj produkcijskoj bazi (koja već ima kategorije,
    /// proizvođače i robu) to znači: kolone postoje, tri tabele postoje, ali su prazne —
    /// <c>GET /korpice</c> vraća <c>[]</c>, svaki tab pokazuje "nije u ponudi", i
    /// "Generiši" nikad ne može ništa proizvesti. Ništa ne baca grešku — kvar je nijem.
    ///
    /// Isti obrazac kao <c>MagacinUBazi</c> migracija (vidi CLAUDE.md, red o Magacin-u):
    /// podaci koji moraju postojati u produkciji idu u migraciju, ne u seeder.
    ///
    /// Idempotentno — sigurno za pokretanje i na bazi gdje je DbSeeder VEĆ unio ove iste
    /// redove (lokalni dev): svaki INSERT ima svoj NOT EXISTS/WHERE guard, pa duplikat
    /// ne nastaje ni za KorpicaKategorije veze ni za Product redove korpica; UPDATE
    /// backfill je sam po sebi idempotentan (postavljanje true na već true ne mijenja
    /// ništa).
    /// </summary>
    public partial class KorpicaPodaci : Migration
    {
        /// <summary>
        /// Isti (Tip, KategorijaSlug) parovi kao u DbSeeder-u — bazen generatora se filtrira
        /// po ovoj vezi (vidi GenerisiSedmicniSadrzajHandler), pa ona mora postojati i na
        /// produkciji da bi "Generiši" imao iz čega da bira.
        /// </summary>
        private static readonly (string Tip, string KategorijaSlug)[] VezeKorpica =
        {
            ("Povrce", "povrce"),
            ("Voce", "voce"),
            ("Mlijecna", "mlijecni-proizvodi"),
            ("Suhomesnata", "meso"),
            ("Domacinska", "mlijecni-proizvodi"),
            ("Domacinska", "meso"),
        };

        /// <summary>Isti 5 tipova × 3 veličine kao u DbSeeder-u, iste cijene/opisi/slike.</summary>
        private static readonly (string Tip, string Naziv, string Opis)[] Definicije =
        {
            ("Povrce", "korpica povrća", "Sezonsko povrće sa Manjače, ubrano istog jutra."),
            ("Voce", "korpica voća", "Sezonsko voće sa domaćih gazdinstava."),
            ("Mlijecna", "mliječna korpica", "Sir, kajmak, mlijeko i pavlaka sa Manjače."),
            ("Suhomesnata", "suhomesnata korpica", "Pečenica, slanina, kobasica i suho meso."),
            ("Domacinska", "domaćinska korpica", "Miješano: komad pečenice, slanina, mlijeko i sir."),
        };

        private static readonly (string V, string Prefiks, decimal Faktor, string Clanovi)[] Velicine =
        {
            ("Mala", "Mala", 1.0m, "Za 1 do 2 člana"),
            ("Srednja", "Srednja", 1.4m, "Za 2 do 4 člana"),
            ("Velika", "Velika", 1.8m, "Za 5 i više članova"),
        };

        private static readonly System.Collections.Generic.Dictionary<string, decimal> OsnovneCijene = new()
        {
            ["Povrce"] = 25m,
            ["Voce"] = 28m,
            ["Mlijecna"] = 32m,
            ["Suhomesnata"] = 45m,
            ["Domacinska"] = 48m,
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> Slike = new()
        {
            ["Povrce"] = "/categories/povrce.jpg",
            ["Voce"] = "/categories/voce.jpg",
            ["Mlijecna"] = "/products/mladi-kravlji-sir.jpg",
            ["Suhomesnata"] = "/products/suha-slanina.jpg",
            ["Domacinska"] = "/hero/hero-domacinstvo.jpg",
        };

        /// <summary>Slugovi svih 15 korpica, istim redoslijedom/formulom kao DbSeeder
        /// (Definicije × Velicine, Slug.From("{Prefiks} {Naziv}")) — deljeno između
        /// Up (insert) i Down (delete), da ne mogu da se razjedine.</summary>
        private static string[] Slugovi()
        {
            var lista = new System.Collections.Generic.List<string>();
            foreach (var (_, naziv, _) in Definicije)
            foreach (var (_, prefiks, _, _) in Velicine)
                lista.Add(Slug.From($"{prefiks} {naziv}"));
            return lista.ToArray();
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----- KorpicaKategorije: veza tip korpice -> kategorija iz koje generator
            // bira bazen (vidi GenerisiSedmicniSadrzajHandler). Idempotentno po (Tip,
            // CategoryId); ako konkretan slug kategorije ne postoji na ovoj bazi (vlasnik
            // je kategorije mogao nazvati drugačije), taj jedan red se samo preskače — ne
            // rušimo cijelu migraciju zbog jedne nepoklopljene kategorije.
            foreach (var (tip, slug) in VezeKorpica)
            {
                migrationBuilder.Sql($@"
INSERT INTO ""KorpicaKategorije"" (""TipKorpice"", ""CategoryId"")
SELECT '{tip}', c.""Id""
FROM ""Categories"" c
WHERE c.""Slug"" = '{slug}'
  AND NOT EXISTS (
      SELECT 1 FROM ""KorpicaKategorije"" k
      WHERE k.""TipKorpice"" = '{tip}' AND k.""CategoryId"" = c.""Id"");
");
            }

            // ----- 15 Product redova (5 tipova × 3 veličine) — identični DbSeeder-ovim po
            // nazivu/cijeni/opisu/slici, da izgled bude isti bez obzira da li je okruženje
            // popunjeno seederom (dev) ili ovom migracijom (produkcija).
            //
            // Category/Producer su za korpicu čist FK placeholder — ToKorpicaDto ih nikad
            // ne vraća (ne prikazuju se nigdje). DbSeeder koristi "Povrće"/Kovačević kao
            // placeholder, ali na STVARNOJ produkcijskoj bazi nema garancije da baš takva
            // kategorija/proizvođač postoje (vlasnik ih je mogao nazvati drugačije) — zato
            // se ovdje placeholder razrješava na PRVU postojeću kategoriju i PRVOG
            // postojećeg proizvođača (bilo koju/koga), ne na konkretan naziv. Ako baza nema
            // ni jednu kategoriju ili ni jednog proizvođača (prazna prodavnica, bez ičega),
            // WHERE guard ispod sprečava insert sa NULL stranim ključem — ta korpica tad
            // ostaje nedostupna dok vlasnik ne unese prvi proizvod, umjesto da obori migraciju.
            foreach (var (tip, naziv, opis) in Definicije)
            foreach (var (v, prefiks, faktor, clanovi) in Velicine)
            {
                var punNaziv = $"{prefiks} {naziv}";
                var slug = Slug.From(punNaziv);
                var cijena = Math.Round(OsnovneCijene[tip] * faktor, 0).ToString(CultureInfo.InvariantCulture);
                var opisPun = $"{opis} {clanovi}.".Replace("'", "''");
                var id = Guid.NewGuid().ToString();

                migrationBuilder.Sql($@"
INSERT INTO ""Products""
    (""Id"", ""Name"", ""Slug"", ""Price"", ""Unit"", ""Stock"", ""IsAvailable"", ""IsFeatured"",
     ""Description"", ""ImageUrl"", ""CategoryId"", ""ProducerId"", ""IsDeleted"", ""CreatedAt"", ""UpdatedAt"",
     ""Vrsta"", ""TipKorpice"", ""Velicina"", ""MozeUKorpicu"", ""PrioritetUKorpici"", ""Mjeseci"")
SELECT '{id}', '{punNaziv.Replace("'", "''")}', '{slug}', {cijena}, 'Kom', 40, true, false,
       '{opisPun}', '{Slike[tip]}', kat.""Id"", proiz.""Id"", false, now(), now(),
       'Korpica', '{tip}', '{v}', false, 0, ''
FROM (SELECT ""Id"" FROM ""Categories"" ORDER BY ""SortOrder"" LIMIT 1) kat,
     (SELECT ""Id"" FROM ""Producers"" ORDER BY ""Name"" LIMIT 1) proiz
WHERE NOT EXISTS (SELECT 1 FROM ""Products"" WHERE ""Slug"" = '{slug}');
");
            }

            // ----- Backfill: obična roba je podobna za sedmične korpice (isti izbor kao
            // DbSeeder pravi za svaki demo proizvod) — bez ovoga MozeUKorpicu ostaje false
            // za SVU postojeću robu na produkciji, pa bazen generatora nema iz čega da bira
            // ni kad KorpicaKategorije i Product redovi iznad postoje.
            migrationBuilder.Sql(@"UPDATE ""Products"" SET ""MozeUKorpicu"" = true WHERE ""Vrsta"" = 'Obican' AND ""MozeUKorpicu"" = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Briše samo redove koje je Up DODAO (korpice po slugu, KorpicaKategorije po
            // (Tip, kategorija-po-slugu)) — prepoznatljivi i sigurni za obrisati bez obzira
            // da li ih je unijela ova migracija ili je DbSeeder stigao prvi (isti slug/par).
            //
            // NAMJERNO ne vraćamo MozeUKorpicu backfill na false: to je podatak, ne šema, i
            // dotad je admin kroz AdminProducts (I1, finalni review) mogao svjesno promijeniti
            // te vrijednosti za konkretne artikle — vraćanje na false bi obrisalo te odluke
            // bez traga. "Down" ovdje znači "ukloni ono što je Up stvorio", ne "poništi svaku
            // posljedicu Up-a".
            foreach (var slug in Slugovi())
            {
                migrationBuilder.Sql($@"DELETE FROM ""Products"" WHERE ""Slug"" = '{slug}' AND ""Vrsta"" = 'Korpica';");
            }

            foreach (var (tip, slug) in VezeKorpica)
            {
                migrationBuilder.Sql($@"
DELETE FROM ""KorpicaKategorije"" k
USING ""Categories"" c
WHERE k.""CategoryId"" = c.""Id"" AND c.""Slug"" = '{slug}' AND k.""TipKorpice"" = '{tip}';
");
            }
        }
    }
}
