using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPoene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreporucioId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreporukaKod",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IskoristeniPoeni",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PoeniPopust",
                table: "Orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PoeniStavke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Iznos = table.Column<int>(type: "integer", nullable: false),
                    Vrsta = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreporuceniId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoeniStavke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoeniStavke_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PoeniStavke_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreporucioId",
                table: "Users",
                column: "PreporucioId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreporukaKod",
                table: "Users",
                column: "PreporukaKod",
                unique: true,
                filter: "\"PreporukaKod\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PoeniStavke_OrderId",
                table: "PoeniStavke",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PoeniStavke_OrderId_Vrsta",
                table: "PoeniStavke",
                columns: new[] { "OrderId", "Vrsta" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PoeniStavke_UserId",
                table: "PoeniStavke",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_PreporucioId",
                table: "Users",
                column: "PreporucioId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_PreporucioId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "PoeniStavke");

            migrationBuilder.DropIndex(
                name: "IX_Users_PreporucioId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PreporukaKod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreporucioId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreporukaKod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IskoristeniPoeni",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PoeniPopust",
                table: "Orders");
        }
    }
}
