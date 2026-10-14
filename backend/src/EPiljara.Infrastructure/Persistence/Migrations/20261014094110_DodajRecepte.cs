using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajRecepte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recepti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Naslov = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Opis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SlikaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VrijemeMinuta = table.Column<int>(type: "integer", nullable: false),
                    Porcije = table.Column<int>(type: "integer", nullable: false),
                    Tezina = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Koraci = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Objavljen = table.Column<bool>(type: "boolean", nullable: false),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recepti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceptSastojci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Naziv = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kolicina = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Redoslijed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceptSastojci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceptSastojci_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReceptSastojci_Recepti_ReceptId",
                        column: x => x.ReceptId,
                        principalTable: "Recepti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recepti_Objavljen",
                table: "Recepti",
                column: "Objavljen");

            migrationBuilder.CreateIndex(
                name: "IX_Recepti_Slug",
                table: "Recepti",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceptSastojci_ProductId",
                table: "ReceptSastojci",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceptSastojci_ReceptId",
                table: "ReceptSastojci",
                column: "ReceptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceptSastojci");

            migrationBuilder.DropTable(
                name: "Recepti");
        }
    }
}
