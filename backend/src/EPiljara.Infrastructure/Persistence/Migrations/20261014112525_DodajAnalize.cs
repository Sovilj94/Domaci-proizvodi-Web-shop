using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajAnalize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Analize",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Uzorak = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Laboratorija = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BrojIzvjestaja = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DatumUzorkovanja = table.Column<DateOnly>(type: "date", nullable: false),
                    Ispitano = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Rezultat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Napomena = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PdfUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Objavljena = table.Column<bool>(type: "boolean", nullable: false),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Analize", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Analize_Producers_ProducerId",
                        column: x => x.ProducerId,
                        principalTable: "Producers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Analize_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Analize_Objavljena_DatumUzorkovanja",
                table: "Analize",
                columns: new[] { "Objavljena", "DatumUzorkovanja" });

            migrationBuilder.CreateIndex(
                name: "IX_Analize_ProducerId",
                table: "Analize",
                column: "ProducerId");

            migrationBuilder.CreateIndex(
                name: "IX_Analize_ProductId",
                table: "Analize",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Analize");
        }
    }
}
