using EPiljara.Application.Common;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>B3: povreda jedinstvenog indeksa pri istovremenom upisu je 409, ne 500.</summary>
public class IstovremenaPromjenaTests
{
    [Fact]
    public async Task Povreda_jedinstvenog_indeksa_postaje_konflikt()
    {
        using var t = new TestDb();
        t.Db.PrijaveNovosti.Add(new PrijavaNovosti { Email = "isti@test.local" });
        t.Db.PrijaveNovosti.Add(new PrijavaNovosti { Email = "isti@test.local" });

        var radnja = () => IstovremenaPromjena.SacuvajAsync(t.Db, default);

        var ex = await radnja.Should().ThrowAsync<ConflictException>();
        ex.Which.Code.Should().Be("istovremena_promjena");
    }

    [Fact]
    public async Task Ispravan_upis_prolazi()
    {
        using var t = new TestDb();
        t.Db.PrijaveNovosti.Add(new PrijavaNovosti { Email = "jedan@test.local" });

        await IstovremenaPromjena.SacuvajAsync(t.Db, default);

        t.Db.PrijaveNovosti.Should().ContainSingle();
    }
}
