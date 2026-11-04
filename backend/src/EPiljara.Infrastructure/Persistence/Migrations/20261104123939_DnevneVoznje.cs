using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DnevneVoznje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DnevneVoznje",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    ProcjenaMetara = table.Column<double>(type: "double precision", nullable: true),
                    ProcjenaSekundi = table.Column<double>(type: "double precision", nullable: true),
                    ProcjenaIzvor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ProcjenaStanica = table.Column<int>(type: "integer", nullable: false),
                    ProcjenaBezKoordinata = table.Column<int>(type: "integer", nullable: false),
                    ProcjenaPotpis = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcjenaIzracunata = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlacenoGorivo = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Litara = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Napomena = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PlacenoUneseno = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlacenoUnio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DnevneVoznje", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DnevneVoznje_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DnevneVoznje_Datum",
                table: "DnevneVoznje",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_DnevneVoznje_DriverId_Datum",
                table: "DnevneVoznje",
                columns: new[] { "DriverId", "Datum" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DnevneVoznje");
        }
    }
}
