using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Špajz po uzoru na farmit: pojedinačno se prodaju korpice, jaja, ajvar, džem, med i mliječno.
    /// Povrće i voće na kilogram (i meso) ostaje u katalogu za korpice, ali izlazi iz Špajza
    /// (<c>USpajzu</c> = false; admin ga može vratiti po proizvodu). Zimnica se dijeli na Ajvar i Džem.
    /// </summary>
    public partial class SpajzKaoFarmit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "USpajzu",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"USpajzu\" = FALSE WHERE \"Vrsta\" = 'Obican' AND \"CategoryId\" IN " +
                "(SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" IN ('povrce', 'voce', 'meso'));");

            // Zimnica → Ajvar i Džem (samo ako zimnica postoji i nove kategorije još ne postoje).
            migrationBuilder.Sql(
                "INSERT INTO \"Categories\" (\"Id\", \"Name\", \"Slug\", \"ImageUrl\", \"SortOrder\", \"NazivEn\") " +
                "SELECT gen_random_uuid(), 'Ajvar', 'ajvar', '/slike/categories/ajvar.jpg', 7, 'Ajvar' " +
                "WHERE EXISTS (SELECT 1 FROM \"Categories\" WHERE \"Slug\" = 'zimnica') " +
                "AND NOT EXISTS (SELECT 1 FROM \"Categories\" WHERE \"Slug\" = 'ajvar');");
            migrationBuilder.Sql(
                "INSERT INTO \"Categories\" (\"Id\", \"Name\", \"Slug\", \"ImageUrl\", \"SortOrder\", \"NazivEn\") " +
                "SELECT gen_random_uuid(), 'Džem', 'dzem', '/slike/categories/dzem.jpg', 8, 'Jam' " +
                "WHERE EXISTS (SELECT 1 FROM \"Categories\" WHERE \"Slug\" = 'zimnica') " +
                "AND NOT EXISTS (SELECT 1 FROM \"Categories\" WHERE \"Slug\" = 'dzem');");
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"CategoryId\" = (SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" = 'ajvar') " +
                "WHERE \"Slug\" LIKE '%ajvar%' AND \"Vrsta\" = 'Obican' " +
                "AND \"CategoryId\" = (SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" = 'zimnica');");
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"CategoryId\" = (SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" = 'dzem') " +
                "WHERE \"Vrsta\" = 'Obican' " +
                "AND \"CategoryId\" = (SELECT \"Id\" FROM \"Categories\" WHERE \"Slug\" = 'zimnica');");
            // Zimnica bez proizvoda više nema smisla; ako je neko još koristi (npr. korpica radi FK-a), ostaje.
            migrationBuilder.Sql(
                "DELETE FROM \"Categories\" WHERE \"Slug\" = 'zimnica' " +
                "AND NOT EXISTS (SELECT 1 FROM \"Products\" p WHERE p.\"CategoryId\" = \"Categories\".\"Id\") " +
                "AND NOT EXISTS (SELECT 1 FROM \"KorpicaKategorije\" k WHERE k.\"CategoryId\" = \"Categories\".\"Id\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Podjela zimnice se ne vraća (nove kategorije ostaju); vraća se samo kolona.
            migrationBuilder.DropColumn(
                name: "USpajzu",
                table: "Products");
        }
    }
}
