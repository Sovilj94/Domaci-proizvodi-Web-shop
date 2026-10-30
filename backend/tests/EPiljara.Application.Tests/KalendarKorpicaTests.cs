using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KalendarKorpicaTests
{
    [Fact]
    public async Task Admin_mijenja_mjesece_i_ukljucuje_kulturu_u_korpicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: false);
        var kopriva = await t.Db.Products.SingleAsync(p => p.Id == t.VanSezoneId);
        kopriva.MozeUKorpicu = false;
        await t.Db.SaveChangesAsync();

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
