using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NarudzbaPrimalac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimalacIme",
                table: "Orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimalacTelefon",
                table: "Orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimalacIme",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PrimalacTelefon",
                table: "Orders");
        }
    }
}
