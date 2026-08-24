using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RouteOptimizationResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DuzinaMetara",
                table: "DeliveryRoutes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Geometrija",
                table: "DeliveryRoutes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TrajanjeSekundi",
                table: "DeliveryRoutes",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuzinaMetara",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "Geometrija",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "TrajanjeSekundi",
                table: "DeliveryRoutes");
        }
    }
}
