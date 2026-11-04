using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TroskoviIGorivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmbalazaCijena",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmbalazaCijena",
                table: "OrderItems",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "KilometrazaMetara",
                table: "DeliveryRoutes",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CijeneGoriva",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaziOd = table.Column<DateOnly>(type: "date", nullable: false),
                    CijenaPoLitru = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    PotrosnjaL100km = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CijeneGoriva", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VrsteTroskova",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Naziv = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    JeGorivo = table.Column<bool>(type: "boolean", nullable: false),
                    Redoslijed = table.Column<int>(type: "integer", nullable: false),
                    Aktivna = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VrsteTroskova", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PonavljajuciTroskovi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VrstaTroskaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Iznos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Opis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OdGodina = table.Column<int>(type: "integer", nullable: false),
                    OdMjesec = table.Column<int>(type: "integer", nullable: false),
                    DoGodina = table.Column<int>(type: "integer", nullable: true),
                    DoMjesec = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PonavljajuciTroskovi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PonavljajuciTroskovi_VrsteTroskova_VrstaTroskaId",
                        column: x => x.VrstaTroskaId,
                        principalTable: "VrsteTroskova",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Troskovi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    VrstaTroskaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Iznos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Opis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RacunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Troskovi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Troskovi_VrsteTroskova_VrstaTroskaId",
                        column: x => x.VrstaTroskaId,
                        principalTable: "VrsteTroskova",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CijeneGoriva_VaziOd",
                table: "CijeneGoriva",
                column: "VaziOd",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PonavljajuciTroskovi_VrstaTroskaId",
                table: "PonavljajuciTroskovi",
                column: "VrstaTroskaId");

            migrationBuilder.CreateIndex(
                name: "IX_Troskovi_Datum",
                table: "Troskovi",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Troskovi_VrstaTroskaId",
                table: "Troskovi",
                column: "VrstaTroskaId");

            migrationBuilder.CreateIndex(
                name: "IX_VrsteTroskova_Naziv",
                table: "VrsteTroskova",
                column: "Naziv",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CijeneGoriva");

            migrationBuilder.DropTable(
                name: "PonavljajuciTroskovi");

            migrationBuilder.DropTable(
                name: "Troskovi");

            migrationBuilder.DropTable(
                name: "VrsteTroskova");

            migrationBuilder.DropColumn(
                name: "AmbalazaCijena",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AmbalazaCijena",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "KilometrazaMetara",
                table: "DeliveryRoutes");
        }
    }
}
