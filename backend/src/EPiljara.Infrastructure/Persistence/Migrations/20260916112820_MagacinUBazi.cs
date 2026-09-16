using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Magacin prelazi iz <c>appsettings.json</c> u bazu, da se lokacija mijenja bez
    /// rebuild-a i deploya (admin panel, <c>PUT /admin/warehouse</c>).
    ///
    /// Red upisuje <b>migracija</b>, a ne <c>DbSeeder</c>: seeder izlazi na prvu
    /// postojeću kategoriju i radi samo u Developmentu ili uz <c>Seed:Demo=true</c>,
    /// pa bi prava produkcija ostala bez magacina — a bez njega API namjerno ne startuje.
    /// </summary>
    public partial class MagacinUBazi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Magacin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Naziv = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Magacin", x => x.Id);
                    table.CheckConstraint("CK_Magacin_JedanRed", "\"Id\" = 'a0000001-0000-4000-8000-000000000001'");
                });

            // Stričići na Manjači, po OpenStreetMap-u (mjesto tipa „village", Grad Banja
            // Luka). Centar sela, ne izmjerena parcela magacina — ali stvarna tačka unutar
            // GeoBounds, koja se od sada mijenja kroz admin panel, bez deploya.
            migrationBuilder.InsertData(
                table: "Magacin",
                columns: ["Id", "Naziv", "Latitude", "Longitude"],
                values: [
                    new Guid("a0000001-0000-4000-8000-000000000001"),
                    "Magacin Stričići",
                    44.577975,
                    17.005162,
                ]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Magacin");
        }
    }
}
