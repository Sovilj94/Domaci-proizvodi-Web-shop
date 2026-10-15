using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPismo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pismo",
                table: "Users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Latinica");

            migrationBuilder.AddColumn<string>(
                name: "Pismo",
                table: "Orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Latinica");

            migrationBuilder.AddColumn<string>(
                name: "Pismo",
                table: "GuestOrderRequests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Latinica");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pismo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Pismo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Pismo",
                table: "GuestOrderRequests");
        }
    }
}
