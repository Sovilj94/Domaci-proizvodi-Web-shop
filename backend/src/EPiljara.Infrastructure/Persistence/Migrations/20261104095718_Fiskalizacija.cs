using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fiskalizacija : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FiskalOznakaBezPdv",
                table: "Podesavanja",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "A");

            migrationBuilder.AddColumn<string>(
                name: "FiskalOznakaPdv",
                table: "Podesavanja",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "E");

            migrationBuilder.AddColumn<bool>(
                name: "FiskalPdvObveznik",
                table: "Podesavanja",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FiskalStopaPdv",
                table: "Podesavanja",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 17m);

            migrationBuilder.CreateTable(
                name: "FiskalniRacuni",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Vrsta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Iznos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NacinPlacanja = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StavkeJson = table.Column<string>(type: "text", nullable: false),
                    RefundiraniRacunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Dobavljac = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Broj = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Brojac = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Izdat = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QrSadrzaj = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VerifikacijaUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Pokusaja = table.Column<int>(type: "integer", nullable: false),
                    ZadnjaGreska = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SljedeciPokusaj = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrajnaGreska = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiskalniRacuni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiskalniRacuni_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiskalniRacuni_OrderId_Vrsta",
                table: "FiskalniRacuni",
                columns: new[] { "OrderId", "Vrsta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiskalniRacuni_Status",
                table: "FiskalniRacuni",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiskalniRacuni");

            migrationBuilder.DropColumn(
                name: "FiskalOznakaBezPdv",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "FiskalOznakaPdv",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "FiskalPdvObveznik",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "FiskalStopaPdv",
                table: "Podesavanja");
        }
    }
}
