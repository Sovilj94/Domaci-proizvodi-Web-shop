using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NabavnaCijenaIZamjene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NabavnaCijena",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ZamjeneSadrzaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SedmicniSadrzajId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    JedinicnaCijena = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZamjeneSadrzaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZamjeneSadrzaja_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZamjeneSadrzaja_SedmicniSadrzaji_SedmicniSadrzajId",
                        column: x => x.SedmicniSadrzajId,
                        principalTable: "SedmicniSadrzaji",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZamjeneSadrzaja_ProductId",
                table: "ZamjeneSadrzaja",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ZamjeneSadrzaja_SedmicniSadrzajId_ProductId",
                table: "ZamjeneSadrzaja",
                columns: new[] { "SedmicniSadrzajId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZamjeneSadrzaja");

            migrationBuilder.DropColumn(
                name: "NabavnaCijena",
                table: "Products");
        }
    }
}
