using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KilometrazaSata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KmKraj",
                table: "DnevneVoznje",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KmPocetak",
                table: "DnevneVoznje",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KmKraj",
                table: "DnevneVoznje");

            migrationBuilder.DropColumn(
                name: "KmPocetak",
                table: "DnevneVoznje");
        }
    }
}
