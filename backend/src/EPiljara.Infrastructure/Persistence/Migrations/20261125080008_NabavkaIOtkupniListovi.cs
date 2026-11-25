using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NabavkaIOtkupniListovi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Adresa",
                table: "Producers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Banka",
                table: "Producers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bpg",
                table: "Producers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Jmbg",
                table: "Producers",
                type: "character varying(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NosilacIme",
                table: "Producers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZiroRacun",
                table: "Producers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ZakljucanaU",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OtkupniListovi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Broj = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProducerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Godina = table.Column<int>(type: "integer", nullable: false),
                    Sedmica = table.Column<int>(type: "integer", nullable: false),
                    Datum = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Zastario = table.Column<bool>(type: "boolean", nullable: false),
                    Napomena = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtkupniListovi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtkupniListovi_Producers_ProducerId",
                        column: x => x.ProducerId,
                        principalTable: "Producers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtkupniListStavke",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OtkupniListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Naziv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Jedinica = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Naruceno = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Cijena = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Primljeno = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    Redoslijed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtkupniListStavke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtkupniListStavke_OtkupniListovi_OtkupniListId",
                        column: x => x.OtkupniListId,
                        principalTable: "OtkupniListovi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtkupniListovi_Broj",
                table: "OtkupniListovi",
                column: "Broj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtkupniListovi_ProducerId_Godina_Sedmica",
                table: "OtkupniListovi",
                columns: new[] { "ProducerId", "Godina", "Sedmica" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtkupniListStavke_OtkupniListId",
                table: "OtkupniListStavke",
                column: "OtkupniListId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtkupniListStavke");

            migrationBuilder.DropTable(
                name: "OtkupniListovi");

            migrationBuilder.DropColumn(
                name: "Adresa",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "Banka",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "Bpg",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "Jmbg",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "NosilacIme",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "ZiroRacun",
                table: "Producers");

            migrationBuilder.DropColumn(
                name: "ZakljucanaU",
                table: "Orders");
        }
    }
}
