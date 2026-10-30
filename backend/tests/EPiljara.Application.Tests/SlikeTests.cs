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
