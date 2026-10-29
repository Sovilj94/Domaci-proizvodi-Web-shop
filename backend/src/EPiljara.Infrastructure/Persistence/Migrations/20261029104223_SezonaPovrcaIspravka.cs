using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ispravka kalendara povrća: pet naših kultura je u <c>SezonaPovrcaKalendar</c> dobilo zbir
    /// mjeseci više srodnih kultura. Sada svaka ima tačno mjesece svog parnjaka, a srodne kulture
    /// (žuta babura, mladi i crveni kupus, boranije, salate, celer) su posebni proizvodi iz
    /// sezonske ponude. Vrijednosti su doslovno ovdje, kao i u prethodnoj migraciji.
    /// </summary>
    public partial class SezonaPovrcaIspravka : Migration
    {
        private static readonly (string Slug, string Mjeseci)[] Ispravke =
        [
            ("babura-paprika", "8,9,10"),
            ("celer-korijen", "4,5,6"),
            ("kupus", "10"),
            ("mahune", "10"),
            ("zelena-salata", "1,2,3,4,5,6,7,10,12"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (slug, mjeseci) in Ispravke)
                migrationBuilder.Sql($"UPDATE \"Products\" SET \"Mjeseci\" = '{mjeseci}' WHERE \"Slug\" = '{slug}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Podaci se ne vraćaju: šema se nije mijenjala.
        }
    }
}
