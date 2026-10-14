using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPartnerPrijave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerPrijave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ime = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Gazdinstvo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Mjesto = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Proizvodi = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AdminNapomena = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Kreirana = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerPrijave", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPrijave_Status_Kreirana",
                table: "PartnerPrijave",
                columns: new[] { "Status", "Kreirana" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerPrijave");
        }
    }
}
