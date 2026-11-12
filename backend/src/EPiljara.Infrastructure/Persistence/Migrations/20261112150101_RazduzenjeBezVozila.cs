using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RazduzenjeBezVozila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Registracija",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "Vozilo",
                table: "Drivers");

            migrationBuilder.AddColumn<decimal>(
                name: "PredanoGotovine",
                table: "DnevneVoznje",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PredanoU",
                table: "DnevneVoznje",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PredanoGotovine",
                table: "DnevneVoznje");

            migrationBuilder.DropColumn(
                name: "PredanoU",
                table: "DnevneVoznje");

            migrationBuilder.AddColumn<string>(
                name: "Registracija",
                table: "Drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vozilo",
                table: "Drivers",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }
    }
}
