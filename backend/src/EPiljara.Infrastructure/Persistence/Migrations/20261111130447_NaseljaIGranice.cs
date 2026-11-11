using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NaseljaIGranice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DostavaBezKorpice",
                table: "Podesavanja",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "DostavaSaKorpicom",
                table: "Podesavanja",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimalnaNarudzba",
                table: "Podesavanja",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "CentarLat",
                table: "DeliveryZones",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CentarLon",
                table: "DeliveryZones",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Granica",
                table: "DeliveryZones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IzvorGranice",
                table: "DeliveryZones",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "procjena");

            migrationBuilder.AddColumn<string>(
                name: "IzvorRedoslijeda",
                table: "DeliveryRoutes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RedniBroj",
                table: "DeliveryRoutes",
                type: "integer",
                nullable: true);

            // Početne granice 24 banjalučka naselja (odluka vlasnika 11.11.2026.: samo grad i
            // bliska prigradska naselja). Postojeće zone istog imena dobijaju granicu, ništa se ne briše.
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql(Podaci.PocetnaNaselja.Sql(Podaci.PocetnaNaselja.Ucitaj()));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DostavaBezKorpice",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "DostavaSaKorpicom",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "MinimalnaNarudzba",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "CentarLat",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "CentarLon",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "Granica",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "IzvorGranice",
                table: "DeliveryZones");

            migrationBuilder.DropColumn(
                name: "IzvorRedoslijeda",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "RedniBroj",
                table: "DeliveryRoutes");
        }
    }
}
