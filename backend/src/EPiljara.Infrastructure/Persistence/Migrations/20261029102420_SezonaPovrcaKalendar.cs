using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Mjeseci povrća za korpice po sezonskom kalendaru (2026-10-29, vidi
    /// <c>DbSeeder.KalendarPovrca</c>). Grašak, kiseli kupus, kopriva i sremuš izlaze iz korpica
    /// (ostaju u Špajzu). Vrijednosti su ovdje doslovno — migracija ne smije zavisiti od koda koji
    /// se kasnije mijenja.
    /// </summary>
    public partial class SezonaPovrcaKalendar : Migration
    {
        private static readonly (string Slug, string Mjeseci)[] Kalendar =
        [
            ("babura-paprika", "7,8,9,10"),
            ("bijeli-luk", "1,2,3,4,6,7,9,10,11,12"),
            ("blitva", "1,2,3,4,10,11,12"),
            ("brokula", "11,12"),
            ("bundeva", "1,3,9,10,11,12"),
            ("celer-korijen", "1,2,4,5,6,7,8"),
            ("crni-luk", "1,2,3,4,5,6,7,8,9,10,11,12"),
            ("cvekla", "1,2,3,4,8,9,10,11,12"),
            ("domaci-krompir", "1,2,3,4,5,6,7,8,9,10,11,12"),
            ("grah-tetovac", "1,2,3,10,11,12"),
            ("karfiol", "11,12"),
            ("keleraba", "2,3,4"),
            ("kelj", "1,2,3,12"),
            ("krastavac", "5,6,7,8,9,10"),
            ("kukuruz-secerac", "8,9,10"),
            ("kupus", "1,2,3,5,6,10,11,12"),
            ("ljuta-paprika", "6,7,8,9,10"),
            ("mahune", "7,8,10"),
            ("mladi-luk", "1,2,3,4,5,6,11,12"),
            ("mrkva", "1,2,3,4,5,6,7,8,9,10,11,12"),
            ("paradajz-iz-plastenika", "6,7,8,9"),
            ("pastrnak", "1,2,3,4,5,6,8,10,11,12"),
            ("patlidzan", "8,9,10"),
            ("persun", "1,2,3,4,5,6,8,10,11,12"),
            ("praziluk", "1,2,3,4,5,9,10,11,12"),
            ("rotkvica", "1,2,3,4,5,10,11,12"),
            ("spanac", "2,3,4,5,10"),
            ("tikvica", "4,5,6,7,8,9,10"),
            ("zelena-salata", "1,2,3,4,5,6,7,10,11,12"),
        ];

        private static readonly string[] VanKorpice = ["grasak", "kiseli-kupus", "kopriva", "sremus"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (slug, mjeseci) in Kalendar)
                migrationBuilder.Sql(
                    $"UPDATE \"Products\" SET \"Mjeseci\" = '{mjeseci}', \"MozeUKorpicu\" = TRUE WHERE \"Slug\" = '{slug}';");
            foreach (var slug in VanKorpice)
                migrationBuilder.Sql($"UPDATE \"Products\" SET \"MozeUKorpicu\" = FALSE WHERE \"Slug\" = '{slug}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Podaci se ne vraćaju: prethodni mjeseci nisu sačuvani, a šema se nije mijenjala.
        }
    }
}
