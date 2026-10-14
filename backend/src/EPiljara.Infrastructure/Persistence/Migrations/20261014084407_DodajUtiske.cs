using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajUtiske : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Utisci",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ime = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Mjesto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Tekst = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Objavljen = table.Column<bool>(type: "boolean", nullable: false),
                    Redoslijed = table.Column<int>(type: "integer", nullable: false),
                    Kreiran = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utisci", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Utisci_Objavljen_Redoslijed",
                table: "Utisci",
                columns: new[] { "Objavljen", "Redoslijed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Utisci");
        }
    }
}
