using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mejlovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OdjavljenOdObavjestenja",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ResetLozinkeHash",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetLozinkeIstice",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavKupacIsporucena",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavKupacNeuspjela",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavKupacOtkazana",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavKupacPotvrdjena",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ObavRadniciPrimaoci",
                table: "Podesavanja",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ObavRadnikFiskalnaGreska",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavRadnikGorivo",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavRadnikNovaNarudzba",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavRadnikOdbijene",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ObavVozacPlan",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ObavVozacPlanSat",
                table: "Podesavanja",
                type: "integer",
                nullable: false,
                defaultValue: 17);

            migrationBuilder.CreateTable(
                name: "EmailKampanje",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Naslov = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tekst = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Grupa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrupaOpis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrojPrimalaca = table.Column<int>(type: "integer", nullable: false),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PoslaoId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailKampanje", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailPoruke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Primalac = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PrimalacIme = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Naslov = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Html = table.Column<string>(type: "text", nullable: false),
                    Vrsta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Kljuc = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    KampanjaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Pokusaja = table.Column<int>(type: "integer", nullable: false),
                    SljedeciPokusaj = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ZadnjaGreska = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Poslata = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DobavljacId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Transport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailPoruke", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailPoruke_KampanjaId",
                table: "EmailPoruke",
                column: "KampanjaId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailPoruke_Kljuc",
                table: "EmailPoruke",
                column: "Kljuc",
                unique: true,
                filter: "\"Kljuc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailPoruke_Kreirana",
                table: "EmailPoruke",
                column: "Kreirana");

            migrationBuilder.CreateIndex(
                name: "IX_EmailPoruke_Status_SljedeciPokusaj",
                table: "EmailPoruke",
                columns: new[] { "Status", "SljedeciPokusaj" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailKampanje");

            migrationBuilder.DropTable(
                name: "EmailPoruke");

            migrationBuilder.DropColumn(
                name: "OdjavljenOdObavjestenja",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetLozinkeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetLozinkeIstice",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ObavKupacIsporucena",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavKupacNeuspjela",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavKupacOtkazana",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavKupacPotvrdjena",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavRadniciPrimaoci",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavRadnikFiskalnaGreska",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavRadnikGorivo",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavRadnikNovaNarudzba",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavRadnikOdbijene",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavVozacPlan",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "ObavVozacPlanSat",
                table: "Podesavanja");
        }
    }
}
