using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlikeUBazi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Slike",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kljuc = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Podaci = table.Column<byte[]>(type: "bytea", nullable: false),
                    Velicina = table.Column<int>(type: "integer", nullable: false),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slike", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slike_Kljuc",
                table: "Slike",
                column: "Kljuc",
                unique: true);

            // Postojeće putanje na statične fajlove frontenda postaju putanje na slike u bazi
            // (SlikeUvoz ih upiše na startu). Vanjski URL-ovi (https://…) ostaju kako jesu.
            foreach (var (tabela, kolona) in Kolone)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{tabela}\" SET \"{kolona}\" = '/slike' || \"{kolona}\" " +
                    $"WHERE \"{kolona}\" LIKE '/products/%' OR \"{kolona}\" LIKE '/categories/%' " +
                    $"OR \"{kolona}\" LIKE '/producers/%';");
                migrationBuilder.Sql(
                    $"UPDATE \"{tabela}\" SET \"{kolona}\" = '/slike/korpice/domacinska.jpg' " +
                    $"WHERE \"{kolona}\" = '/hero/hero-domacinstvo.jpg';");
            }
        }

        private static readonly (string Tabela, string Kolona)[] Kolone =
        [
            ("Products", "ImageUrl"),
            ("Categories", "ImageUrl"),
            ("OrderItems", "ImageUrl"),
            ("GuestOrderRequestItems", "ImageUrl"),
            ("Producers", "PhotoUrl"),
            ("Recepti", "SlikaUrl"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (tabela, kolona) in Kolone)
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{tabela}\" SET \"{kolona}\" = '/hero/hero-domacinstvo.jpg' " +
                    $"WHERE \"{kolona}\" = '/slike/korpice/domacinska.jpg';");
                migrationBuilder.Sql(
                    $"UPDATE \"{tabela}\" SET \"{kolona}\" = substring(\"{kolona}\" from 7) " +
                    $"WHERE \"{kolona}\" LIKE '/slike/products/%' OR \"{kolona}\" LIKE '/slike/categories/%' " +
                    $"OR \"{kolona}\" LIKE '/slike/producers/%';");
            }

            migrationBuilder.DropTable(
                name: "Slike");
        }
    }
}
