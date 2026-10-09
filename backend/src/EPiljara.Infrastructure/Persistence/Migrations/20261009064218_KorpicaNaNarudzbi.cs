using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KorpicaNaNarudzbi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StavkeKorpiceNarudzbe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Kolicina = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    JedinicnaCijena = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DodaoKupac = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StavkeKorpiceNarudzbe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StavkeKorpiceNarudzbe_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StavkeKorpiceNarudzbe_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StavkeKorpiceNarudzbe_OrderItemId",
                table: "StavkeKorpiceNarudzbe",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StavkeKorpiceNarudzbe_ProductId",
                table: "StavkeKorpiceNarudzbe",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StavkeKorpiceNarudzbe");
        }
    }
}
