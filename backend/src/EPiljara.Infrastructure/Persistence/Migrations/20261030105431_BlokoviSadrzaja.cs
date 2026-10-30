using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlokoviSadrzaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlokoviSadrzaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sekcija = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Redoslijed = table.Column<int>(type: "integer", nullable: false),
                    Ikona = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Naslov = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Tekst = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Dugme = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Link = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SlikaUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Boja = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    BojaTeksta = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    NaslovEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TekstEn = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DugmeEn = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Objavljen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlokoviSadrzaja", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlokoviSadrzaja_Sekcija_Redoslijed",
                table: "BlokoviSadrzaja",
                columns: new[] { "Sekcija", "Redoslijed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlokoviSadrzaja");
        }
    }
}
