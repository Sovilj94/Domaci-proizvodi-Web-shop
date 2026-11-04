using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CijenaUPiljari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CijenaPiljara",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CijenaPiljara",
                table: "Products");
        }
    }
}
