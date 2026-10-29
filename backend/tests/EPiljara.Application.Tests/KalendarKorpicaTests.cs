using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KalendarKorpicaTests
{
    private sealed class LazniHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string hash) => password == hash;
    }

    [Fact]
    public async Task Kalendar_povrca_ima_samo_kulture_iz_kategorija_povrca_i_prati_farmit_listu()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());

        var kalendar = await new GetKalendarKorpicaHandler(t.Db).Handle(new GetKalendarKorpicaQuery("povrce"), default);

        kalendar.Should().Contain(k => k.Naziv == "Mrkva" && k.UKorpici);
        kalendar.Should().NotContain(k => k.Naziv == "Jabuka idared");
        kalendar.Single(k => k.Naziv == "Brokula").Mjeseci.Should().Equal(11, 12);
        kalendar.Single(k => k.Naziv == "Kopriva").UKorpici.Should().BeFalse();
        // U korpici prvo, pa ostale.
        kalendar.TakeWhile(k => k.UKorpici).Count().Should().Be(kalendar.Count(k => k.UKorpici));
    }

    [Fact]
    public async Task Admin_mijenja_mjesece_i_ukljucuje_kulturu_u_korpicu()
    {
        using var t = new TestDb();
        await DbSeeder.SeedAsync(t.Db, new LazniHasher());
        var kopriva = await t.Db.Products.SingleAsync(p => p.Slug == "kopriva");

        var dto = await new PostaviKalendarHandler(t.Db).Handle(
            new PostaviKalendarCommand(kopriva.Id, true, [5, 3, 3, 4]), default);

        dto.UKorpici.Should().BeTrue();
        dto.Mjeseci.Should().Equal(3, 4, 5);
        t.Db.ChangeTracker.Clear();
        var sacuvan = await t.Db.Products.SingleAsync(p => p.Id == kopriva.Id);
        sacuvan.MozeUKorpicu.Should().BeTrue();
        sacuvan.Mjeseci.Should().Equal(3, 4, 5);
    }

    [Fact]
    public async Task Nepoznata_vrsta_i_nepostojeci_proizvod_su_404_a_mjesec_13_se_odbija()
    {
        using var t = new TestDb();
        await FluentActions.Invoking(() => new GetKalendarKorpicaHandler(t.Db).Handle(new GetKalendarKorpicaQuery("cvijece"), default))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Invoking(() => new PostaviKalendarHandler(t.Db).Handle(new PostaviKalendarCommand(Guid.NewGuid(), true, [1]), default))
            .Should().ThrowAsync<NotFoundException>();
        new PostaviKalendarValidator().Validate(new PostaviKalendarCommand(Guid.NewGuid(), true, [13])).IsValid.Should().BeFalse();
    }
}
