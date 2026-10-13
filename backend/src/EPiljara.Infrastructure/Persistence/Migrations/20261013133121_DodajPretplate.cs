using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DodajPretplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PretplataId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pretplate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeliveryZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Napomena = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BrojIsporuka = table.Column<int>(type: "integer", nullable: false),
                    PauzaDoGodina = table.Column<int>(type: "integer", nullable: true),
                    PauzaDoSedmica = table.Column<int>(type: "integer", nullable: true),
                    PoslednjaGodina = table.Column<int>(type: "integer", nullable: true),
                    PoslednjaSedmica = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OtkazanaU = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pretplate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pretplate_DeliverySlots_DeliverySlotId",
                        column: x => x.DeliverySlotId,
                        principalTable: "DeliverySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pretplate_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "DeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pretplate_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pretplate_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PretplataId",
                table: "Orders",
                column: "PretplataId");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_DeliverySlotId",
                table: "Pretplate",
                column: "DeliverySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_DeliveryZoneId",
                table: "Pretplate",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_ProductId",
                table: "Pretplate",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_Status",
                table: "Pretplate",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_UserId",
                table: "Pretplate",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pretplate_UserId_ProductId_Aktivne",
                table: "Pretplate",
                columns: new[] { "UserId", "ProductId" },
                unique: true,
                filter: "\"Status\" <> 'Otkazana'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pretplate");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PretplataId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PretplataId",
                table: "Orders");
        }
    }
}
