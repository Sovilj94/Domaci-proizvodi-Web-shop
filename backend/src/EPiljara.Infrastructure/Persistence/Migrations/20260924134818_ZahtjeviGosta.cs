using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZahtjeviGosta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestOrderRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: false),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    DeliveryZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverySlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestOrderRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestOrderRequests_DeliverySlots_DeliverySlotId",
                        column: x => x.DeliverySlotId,
                        principalTable: "DeliverySlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestOrderRequests_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "DeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuestOrderRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestOrderRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestOrderRequestItems_GuestOrderRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "GuestOrderRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequestItems_RequestId",
                table: "GuestOrderRequestItems",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequests_DeliverySlotId",
                table: "GuestOrderRequests",
                column: "DeliverySlotId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequests_DeliveryZoneId",
                table: "GuestOrderRequests",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequests_ExpiresAt",
                table: "GuestOrderRequests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GuestOrderRequests_Token",
                table: "GuestOrderRequests",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestOrderRequestItems");

            migrationBuilder.DropTable(
                name: "GuestOrderRequests");
        }
    }
}
