using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajBojuINovosti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Boja",
                table: "Products",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrijaveNovosti",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrijaveNovosti", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrijaveNovosti_Email",
                table: "PrijaveNovosti",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrijaveNovosti");

            migrationBuilder.DropColumn(
                name: "Boja",
                table: "Products");
        }
    }
}
