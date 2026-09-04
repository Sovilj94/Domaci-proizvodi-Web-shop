using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FullAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DeliveryZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "DeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Addresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_DeliveryZoneId",
                table: "Addresses",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserId",
                table: "Addresses",
                column: "UserId");

            // Prenos postojećih adresa sa korisnika u adresar. Koordinate ostaju NULL:
            // te adrese nikad nisu imale pin i izmišljanje tačke bi poslalo vozača
            // na pogrešno mjesto. Kolone na Users se OVDJE ne diraju — brišu se tek
            // u zadnjem zadatku, kad novi put bude dokazan.
            migrationBuilder.Sql("""
                INSERT INTO "Addresses" ("Id", "UserId", "Label", "FullAddress",
                                         "Latitude", "Longitude", "DeliveryZoneId",
                                         "IsDefault", "CreatedAt")
                SELECT gen_random_uuid(), u."Id", NULL, u."Address",
                       NULL, NULL, u."DeliveryZoneId",
                       TRUE, NOW() AT TIME ZONE 'UTC'
                FROM "Users" u
                WHERE u."Address" IS NOT NULL AND btrim(u."Address") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");
        }
    }
}
