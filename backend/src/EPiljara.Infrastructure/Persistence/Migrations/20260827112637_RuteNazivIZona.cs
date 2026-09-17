using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RuteNazivIZona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_DriverId_Date",
                table: "DeliveryRoutes");

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryZoneId",
                table: "DeliveryRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Naziv",
                table: "DeliveryRoutes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_DeliveryZoneId",
                table: "DeliveryRoutes",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_DriverId",
                table: "DeliveryRoutes",
                column: "DriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryRoutes_DeliveryZones_DeliveryZoneId",
                table: "DeliveryRoutes",
                column: "DeliveryZoneId",
                principalTable: "DeliveryZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryRoutes_DeliveryZones_DeliveryZoneId",
                table: "DeliveryRoutes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_DeliveryZoneId",
                table: "DeliveryRoutes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_DriverId",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "DeliveryZoneId",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "Naziv",
                table: "DeliveryRoutes");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_DriverId_Date",
                table: "DeliveryRoutes",
                columns: new[] { "DriverId", "Date" });
        }
    }
}
