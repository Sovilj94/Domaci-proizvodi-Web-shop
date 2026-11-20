using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TezinaKorpice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TezinaDo",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TezinaKomada",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TezinaOd",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            // Rasponi koje je vlasnik zadao 20.11.2026. za korpice povrća; ostale vrste ostaju bez
            // granice dok ih ne unese u Postavkama korpica.
            migrationBuilder.Sql("""
                UPDATE "Products" SET "TezinaOd" = 3, "TezinaDo" = 5
                 WHERE "Vrsta" = 'Korpica' AND "TipKorpice" = 'Povrce' AND "Velicina" = 'Mala';
                UPDATE "Products" SET "TezinaOd" = 6, "TezinaDo" = 8
                 WHERE "Vrsta" = 'Korpica' AND "TipKorpice" = 'Povrce' AND "Velicina" = 'Srednja';
                UPDATE "Products" SET "TezinaOd" = 8, "TezinaDo" = 10
                 WHERE "Vrsta" = 'Korpica' AND "TipKorpice" = 'Povrce' AND "Velicina" = 'Velika';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TezinaDo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TezinaKomada",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TezinaOd",
                table: "Products");
        }
    }
}
