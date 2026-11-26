using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SezonaVoca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SezonaVocaDo",
                table: "Podesavanja",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SezonaVocaOd",
                table: "Podesavanja",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SezonaVocaDo",
                table: "Podesavanja");

            migrationBuilder.DropColumn(
                name: "SezonaVocaOd",
                table: "Podesavanja");
        }
    }
}
