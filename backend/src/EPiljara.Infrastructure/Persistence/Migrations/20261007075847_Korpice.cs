using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Korpice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Mjeseci",
                table: "Products",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MozeUKorpicu",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PrioritetUKorpici",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TipKorpice",
                table: "Products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Velicina",
                table: "Products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vrsta",
                table: "Products",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Obican");

            migrationBuilder.CreateTable(
                name: "KorpicaKategorije",
                columns: table => new
                {
                    TipKorpice = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KorpicaKategorije", x => new { x.TipKorpice, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_KorpicaKategorije_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SedmicniSadrzaji",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KorpicaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Godina = table.Column<int>(type: "integer", nullable: false),
                    Sedmica = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CiljnaVrijednost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ObjavljenU = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ZakljucanU = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SedmicniSadrzaji", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SedmicniSadrzaji_Products_KorpicaId",
                        column: x => x.KorpicaId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StavkeSadrzaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SedmicniSadrzajId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kolicina = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    JedinicnaCijena = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StavkeSadrzaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StavkeSadrzaja_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StavkeSadrzaja_SedmicniSadrzaji_SedmicniSadrzajId",
                        column: x => x.SedmicniSadrzajId,
                        principalTable: "SedmicniSadrzaji",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Vrsta",
                table: "Products",
                column: "Vrsta");

            migrationBuilder.CreateIndex(
                name: "IX_KorpicaKategorije_CategoryId",
                table: "KorpicaKategorije",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SedmicniSadrzaji_Godina_Sedmica",
                table: "SedmicniSadrzaji",
                columns: new[] { "Godina", "Sedmica" });

            migrationBuilder.CreateIndex(
                name: "IX_SedmicniSadrzaji_KorpicaId_Godina_Sedmica",
                table: "SedmicniSadrzaji",
                columns: new[] { "KorpicaId", "Godina", "Sedmica" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StavkeSadrzaja_ProductId",
                table: "StavkeSadrzaja",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StavkeSadrzaja_SedmicniSadrzajId",
                table: "StavkeSadrzaja",
                column: "SedmicniSadrzajId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KorpicaKategorije");

            migrationBuilder.DropTable(
                name: "StavkeSadrzaja");

            migrationBuilder.DropTable(
                name: "SedmicniSadrzaji");

            migrationBuilder.DropIndex(
                name: "IX_Products_Vrsta",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Mjeseci",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MozeUKorpicu",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrioritetUKorpici",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TipKorpice",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Velicina",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Vrsta",
                table: "Products");
        }
    }
}
