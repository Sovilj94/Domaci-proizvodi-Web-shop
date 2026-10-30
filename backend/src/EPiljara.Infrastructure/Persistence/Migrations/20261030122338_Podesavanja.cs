using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Podesavanja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Podesavanja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KoeficijentVrijednosti = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    MinimalnaMarza = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    BrojZamjena = table.Column<int>(type: "integer", nullable: false),
                    MinArtikalaMala = table.Column<int>(type: "integer", nullable: false),
                    MinArtikalaSrednja = table.Column<int>(type: "integer", nullable: false),
                    MinArtikalaVelika = table.Column<int>(type: "integer", nullable: false),
                    RokSatiRezerva = table.Column<int>(type: "integer", nullable: false),
                    DanIsporukeRezerva = table.Column<int>(type: "integer", nullable: false),
                    VrijemeIsporukeRezerva = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    PopustNedjeljna = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    PopustMjesecna = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    PopustTriMjeseca = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    ObavezaMjesecna = table.Column<int>(type: "integer", nullable: false),
                    ObavezaTriMjeseca = table.Column<int>(type: "integer", nullable: false),
                    SatiUnaprijed = table.Column<int>(type: "integer", nullable: false),
                    AlarmSatiPrijeRoka = table.Column<int>(type: "integer", nullable: false),
                    MaksPauzaSedmica = table.Column<int>(type: "integer", nullable: false),
                    PoenaPoKM = table.Column<int>(type: "integer", nullable: false),
                    VrijednostPoenaKM = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    NagradaPreporuka = table.Column<int>(type: "integer", nullable: false),
                    PragPriKraju = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Podesavanja", x => x.Id);
                    table.CheckConstraint("CK_Podesavanja_JedanRed", "\"Id\" = 'A0000002-0000-4000-8000-000000000001'");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Podesavanja");
        }
    }
}
