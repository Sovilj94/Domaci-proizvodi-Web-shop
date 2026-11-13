using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DostavaDetaljiIspravka : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiskalniRacuni_OrderId_Vrsta",
                table: "FiskalniRacuni");

            migrationBuilder.AddColumn<string>(
                name: "RazlogNeuspjeha",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Refundiran",
                table: "FiskalniRacuni",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Prometni računi koje je admin već refundirao dobijaju oznaku, da ih novi indeks ne broji.
            migrationBuilder.Sql(
                "UPDATE \"FiskalniRacuni\" SET \"Refundiran\" = TRUE WHERE \"Id\" IN " +
                "(SELECT \"RefundiraniRacunId\" FROM \"FiskalniRacuni\" WHERE \"RefundiraniRacunId\" IS NOT NULL);");

            migrationBuilder.CreateIndex(
                name: "IX_FiskalniRacuni_OrderId_VazeciPromet",
                table: "FiskalniRacuni",
                column: "OrderId",
                unique: true,
                filter: "\"Vrsta\" = 'Promet' AND NOT \"Refundiran\"");

            migrationBuilder.CreateIndex(
                name: "IX_FiskalniRacuni_RefundiraniRacunId",
                table: "FiskalniRacuni",
                column: "RefundiraniRacunId",
                unique: true,
                filter: "\"RefundiraniRacunId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiskalniRacuni_OrderId_VazeciPromet",
                table: "FiskalniRacuni");

            migrationBuilder.DropIndex(
                name: "IX_FiskalniRacuni_RefundiraniRacunId",
                table: "FiskalniRacuni");

            migrationBuilder.DropColumn(
                name: "RazlogNeuspjeha",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Refundiran",
                table: "FiskalniRacuni");

            migrationBuilder.CreateIndex(
                name: "IX_FiskalniRacuni_OrderId_Vrsta",
                table: "FiskalniRacuni",
                columns: new[] { "OrderId", "Vrsta" },
                unique: true);
        }
    }
}
