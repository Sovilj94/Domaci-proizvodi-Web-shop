using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KolicineUKorpici : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "KolicinaMala",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KolicinaSrednja",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "KolicinaVelika",
                table: "Products",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KolicinaMala",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KolicinaSrednja",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "KolicinaVelika",
                table: "Products");
        }
    }
}
