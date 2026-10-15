using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPromoKodove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Popust",
                table: "Orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PromoKod",
                table: "Orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoKodId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Popust",
                table: "GuestOrderRequests",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PromoKod",
                table: "GuestOrderRequests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoKodId",
                table: "GuestOrderRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromoKodovi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Opis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Tip = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Vrijednost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MinIznos = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    VaziOd = table.Column<DateOnly>(type: "date", nullable: true),
                    VaziDo = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxKoristenja = table.Column<int>(type: "integer", nullable: true),
                    JednomPoKupcu = table.Column<bool>(type: "boolean", nullable: false),
                    SamoNoviKupci = table.Column<bool>(type: "boolean", nullable: false),
                    Aktivan = table.Column<bool>(type: "boolean", nullable: false),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoKodovi", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PromoKodId",
                table: "Orders",
                column: "PromoKodId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequests_PromoKodId",
                table: "GuestOrderRequests",
                column: "PromoKodId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoKodovi_Kod",
                table: "PromoKodovi",
                column: "Kod",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GuestOrderRequests_PromoKodovi_PromoKodId",
                table: "GuestOrderRequests",
                column: "PromoKodId",
                principalTable: "PromoKodovi",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_PromoKodovi_PromoKodId",
                table: "Orders",
                column: "PromoKodId",
                principalTable: "PromoKodovi",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuestOrderRequests_PromoKodovi_PromoKodId",
                table: "GuestOrderRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_PromoKodovi_PromoKodId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "PromoKodovi");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PromoKodId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_GuestOrderRequests_PromoKodId",
                table: "GuestOrderRequests");

            migrationBuilder.DropColumn(
                name: "Popust",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromoKod",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromoKodId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Popust",
                table: "GuestOrderRequests");

            migrationBuilder.DropColumn(
                name: "PromoKod",
                table: "GuestOrderRequests");

            migrationBuilder.DropColumn(
                name: "PromoKodId",
                table: "GuestOrderRequests");
        }
    }
}
