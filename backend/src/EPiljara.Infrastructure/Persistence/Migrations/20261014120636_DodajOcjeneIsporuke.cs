using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajOcjeneIsporuke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OcjenaId",
                table: "Utisci",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OcjeneIsporuke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ocjena = table.Column<int>(type: "integer", nullable: false),
                    Komentar = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DozvolaZaObjavu = table.Column<bool>(type: "boolean", nullable: false),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcjeneIsporuke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcjeneIsporuke_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Utisci_OcjenaId",
                table: "Utisci",
                column: "OcjenaId",
                unique: true,
                filter: "\"OcjenaId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OcjeneIsporuke_Kreirana",
                table: "OcjeneIsporuke",
                column: "Kreirana");

            migrationBuilder.CreateIndex(
                name: "IX_OcjeneIsporuke_OrderId",
                table: "OcjeneIsporuke",
                column: "OrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Utisci_OcjeneIsporuke_OcjenaId",
                table: "Utisci",
                column: "OcjenaId",
                principalTable: "OcjeneIsporuke",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Utisci_OcjeneIsporuke_OcjenaId",
                table: "Utisci");

            migrationBuilder.DropTable(
                name: "OcjeneIsporuke");

            migrationBuilder.DropIndex(
                name: "IX_Utisci_OcjenaId",
                table: "Utisci");

            migrationBuilder.DropColumn(
                name: "OcjenaId",
                table: "Utisci");
        }
    }
}
