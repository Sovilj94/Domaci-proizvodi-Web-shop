using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Dvije stanice iste rute ne smiju stajati na istom broju. Constraint je
    /// <c>DEFERRABLE INITIALLY DEFERRED</c>, i to je suština: prenumerisanje u
    /// <c>Optimizacija.PrimijeniAsync</c> dodjeljuje otvorenim stanicama isti raspon
    /// brojeva u drugom redoslijedu, a EF to izda kao zaseban UPDATE po redu. Obična
    /// unique provjera (po naredbi) pala bi na prvoj permutaciji, tj. na svakoj
    /// legitimnoj reoptimizaciji; odgođena provjera na COMMIT propušta prolazni sudar
    /// unutar transakcije, a ne dozvoljava da se sudar upiše.
    ///
    /// Pisano sirovim SQL-om jer Npgsql fluent API ne izražava DEFERRABLE, pa
    /// constraint namjerno ne postoji u EF modelu — vidi komentar u AppDbContext.
    /// </summary>
    public partial class RouteStopSequenceUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baza koja duplikate već ima (do sada ih ništa nije branilo) inače ne bi
            // mogla primiti constraint. Viškovi se guraju iznad najvećeg broja svoje
            // rute — deterministički, po Id, da dva pokretanja daju isti rezultat.
            migrationBuilder.Sql(
                """
                WITH dupli AS (
                    SELECT "Id",
                           "RouteId",
                           row_number() OVER (PARTITION BY "RouteId", "Sequence" ORDER BY "Id") AS rbr
                    FROM "RouteStops"
                ),
                maksimumi AS (
                    SELECT "RouteId", max("Sequence") AS najveci
                    FROM "RouteStops"
                    GROUP BY "RouteId"
                ),
                za_pomjeranje AS (
                    SELECT "Id",
                           "RouteId",
                           row_number() OVER (PARTITION BY "RouteId" ORDER BY "Id") AS pomak
                    FROM dupli
                    WHERE rbr > 1
                )
                UPDATE "RouteStops" s
                SET "Sequence" = m.najveci + z.pomak
                FROM za_pomjeranje z
                JOIN maksimumi m ON m."RouteId" = z."RouteId"
                WHERE s."Id" = z."Id";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "RouteStops"
                    ADD CONSTRAINT "UQ_RouteStops_RouteId_Sequence"
                    UNIQUE ("RouteId", "Sequence") DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "RouteStops"
                    DROP CONSTRAINT "UQ_RouteStops_RouteId_Sequence";
                """);
        }
    }
}
