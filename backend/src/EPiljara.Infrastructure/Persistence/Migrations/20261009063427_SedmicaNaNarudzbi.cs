using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SedmicaNaNarudzbi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Godina",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Sedmica",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Godina_Sedmica",
                table: "Orders",
                columns: new[] { "Godina", "Sedmica" });

            // Narudžbama otprije P2b upisujemo ISO sedmicu njihovog CreatedAt. To je
            // PRIBLIŽNO — narudžba napravljena u nedjelju za četvrtak naredne sedmice dobija
            // pogrešnu sedmicu. Prihvata se jer nijedna od njih ne nosi sadržaj korpice; vidi
            // komentar na Order.Godina.
            //
            // U Postgresu je EXTRACT(WEEK ...) ISO sedmica, a ISOYEAR odgovarajuća ISO godina
            // — par koji se mora uzimati zajedno, jer se 1. januar često vodi pod sedmicu 52
            // prethodne ISO godine.
            //
            // Testovi rade na SQLite preko EnsureCreated() i migracije uopšte ne izvršavaju,
            // pa ovaj SQL ne pokriva nijedan test. Provjerava se na Postgresu.
            migrationBuilder.Sql("""
                UPDATE "Orders"
                SET "Godina"  = EXTRACT(ISOYEAR FROM "CreatedAt")::int,
                    "Sedmica" = EXTRACT(WEEK    FROM "CreatedAt")::int
                WHERE "Sedmica" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Godina_Sedmica",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Godina",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Sedmica",
                table: "Orders");
        }
    }
}
