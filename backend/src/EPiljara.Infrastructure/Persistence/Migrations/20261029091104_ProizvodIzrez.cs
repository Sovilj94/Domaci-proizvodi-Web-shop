using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EPiljara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProizvodIzrez : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IzrezUrl",
                table: "Products",
                type: "text",
                nullable: true);

            // Korpice povrća i voća dobijaju svoje fotografije umjesto slike kategorije — samo
            // tamo gdje admin sliku nije mijenjao (SlikeUvoz upiše fajlove na startu).
            foreach (var tip in new[] { "povrce", "voce" })
            foreach (var v in new[] { "mala", "srednja", "velika" })
            {
                var slug = tip == "povrce" ? $"{v}-korpica-povrca" : $"{v}-korpica-voca";
                migrationBuilder.Sql(
                    $"UPDATE \"Products\" SET \"ImageUrl\" = '/slike/korpice/{tip}-{v}.jpg' " +
                    $"WHERE \"Slug\" = '{slug}' AND \"ImageUrl\" = '/slike/categories/{tip}.jpg';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tip in new[] { "povrce", "voce" })
                migrationBuilder.Sql(
                    $"UPDATE \"Products\" SET \"ImageUrl\" = '/slike/categories/{tip}.jpg' " +
                    $"WHERE \"ImageUrl\" LIKE '/slike/korpice/{tip}-%';");

            migrationBuilder.DropColumn(
                name: "IzrezUrl",
                table: "Products");
        }
    }
}
