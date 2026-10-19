using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajObavijestPreskocene : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ObavijestPreskocenaGodina",
                table: "Pretplate",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObavijestPreskocenaSedmica",
                table: "Pretplate",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObavijestPreskocenaGodina",
                table: "Pretplate");

            migrationBuilder.DropColumn(
                name: "ObavijestPreskocenaSedmica",
                table: "Pretplate");
        }
    }
}
