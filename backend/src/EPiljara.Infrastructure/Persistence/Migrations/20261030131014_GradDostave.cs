using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GradDostave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradDostave",
                table: "Podesavanja",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Postojeći red dobija grad u kojem piljara danas dostavlja; dalje ga mijenja admin
            // (`/admin/settings`). Nova baza red dobija iz dumpa, kao i ostala pravila.
            migrationBuilder.Sql("UPDATE \"Podesavanja\" SET \"GradDostave\" = 'Banja Luka';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradDostave",
                table: "Podesavanja");
        }
    }
}
