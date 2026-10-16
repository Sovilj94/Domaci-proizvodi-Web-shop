using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPlanPretplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ObavezaIsporuka",
                table: "Pretplate",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Pretplate",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Nedjeljna");

            // R2: prodaju se samo korpice povrća i voća. Ostale vrste se sakrivaju (ne brišu) —
            // vraćaju se uključivanjem dostupnosti u adminu.
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"IsAvailable\" = false " +
                "WHERE \"Vrsta\" = 'Korpica' AND \"TipKorpice\" IN ('Mlijecna', 'Suhomesnata', 'Domacinska');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObavezaIsporuka",
                table: "Pretplate");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Pretplate");
        }
    }
}
