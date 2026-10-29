using EPiljara.Application.Features.Slike;
using EPiljara.Domain.Common;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class SlikeTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public async Task Uvoz_upisuje_samo_slike_kojih_nema_i_ne_dira_postojece()
    {
        var folder = Directory.CreateTempSubdirectory("seedslike").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "products"));
            await File.WriteAllBytesAsync(Path.Combine(folder, "products", "kajmak.jpg"), Jpeg);
            await File.WriteAllBytesAsync(Path.Combine(folder, "products", "med.png"), [1, 2]);
            await File.WriteAllTextAsync(Path.Combine(folder, "products", "README.md"), "nije slika");

            using var t = new TestDb();
            t.Db.Slike.Add(new Domain.Entities.Slika { Kljuc = "products/med.png", ContentType = "image/png", Podaci = [9], Velicina = 1 });
            await t.Db.SaveChangesAsync();

            var prvi = await SlikeUvoz.UveziAsync(t.Db, folder);
            var drugi = await SlikeUvoz.UveziAsync(t.Db, folder);

            prvi.Should().Be(1);
            drugi.Should().Be(0);
            var slike = await t.Db.Slike.OrderBy(s => s.Kljuc).ToListAsync();
            slike.Select(s => s.Kljuc).Should().Equal("products/kajmak.jpg", "products/med.png");
            slike[0].ContentType.Should().Be("image/jpeg");
            slike[0].Podaci.Should().Equal(Jpeg);
            slike[1].Podaci.Should().Equal([9]); // admin verzija ostaje
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Slika_se_cita_po_kljucu_a_nepostojeca_je_404()
    {
        using var t = new TestDb();
        t.Db.Slike.Add(new Domain.Entities.Slika { Kljuc = "products/kajmak.jpg", Podaci = Jpeg, Velicina = Jpeg.Length });
        await t.Db.SaveChangesAsync();
        var handler = new GetSlikaHandler(t.Db);

        var s = await handler.Handle(new GetSlikaQuery("/products/kajmak.jpg"), default);

        s.Podaci.Should().Equal(Jpeg);
        s.ContentType.Should().Be("image/jpeg");
        await FluentActions.Invoking(() => handler.Handle(new GetSlikaQuery("products/nema.jpg"), default))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Upload_pravi_novi_kljuc_svaki_put_i_vraca_url_za_polje_slike()
    {
        using var t = new TestDb();
        var handler = new SacuvajSlikuHandler(t.Db);

        var prva = await handler.Handle(new SacuvajSlikuCommand("products", "Domaći Kajmak.JPG", "image/jpeg", Jpeg), default);
        var druga = await handler.Handle(new SacuvajSlikuCommand("products", "Domaći Kajmak.JPG", "image/jpeg", Jpeg), default);

        prva.Url.Should().StartWith("/slike/products/domaci-kajmak-").And.EndWith(".jpg");
        prva.Url.Should().Be("/slike/" + prva.Kljuc);
        druga.Kljuc.Should().NotBe(prva.Kljuc);
        (await t.Db.Slike.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("products", "image/gif", 10, "fajl")]
    [InlineData("nepoznat", "image/jpeg", 10, "folder")]
    [InlineData("products", "image/jpeg", 0, "fajl")]
    [InlineData("products", "image/jpeg", SlikePravila.MaxBajtova + 1, "fajl")]
    public void Upload_odbija_pogresan_tip_folder_i_velicinu(string folder, string tip, int bajtova, string polje)
    {
        var rezultat = new SacuvajSlikuValidator().Validate(
            new SacuvajSlikuCommand(folder, "a.jpg", tip, new byte[bajtova]));

        rezultat.IsValid.Should().BeFalse();
        rezultat.Errors.Should().Contain(e => e.PropertyName == polje);
    }
}
