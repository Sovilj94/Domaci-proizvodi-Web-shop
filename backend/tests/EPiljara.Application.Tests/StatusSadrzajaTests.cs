using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

public class StatusSadrzajaTests
{
    [Theory]
    [InlineData(StatusSadrzaja.Nacrt, StatusSadrzaja.Objavljen)]
    [InlineData(StatusSadrzaja.Objavljen, StatusSadrzaja.Zakljucan)]
    public void Dozvoljeni_prelazi_prolaze(StatusSadrzaja iz, StatusSadrzaja u)
    {
        var s = new SedmicniSadrzaj { Status = iz };
        s.Predji(u);
        Assert.Equal(u, s.Status);
    }

    [Theory]
    [InlineData(StatusSadrzaja.Objavljen, StatusSadrzaja.Nacrt)]
    [InlineData(StatusSadrzaja.Zakljucan, StatusSadrzaja.Objavljen)]
    [InlineData(StatusSadrzaja.Nacrt, StatusSadrzaja.Zakljucan)]
    [InlineData(StatusSadrzaja.Nacrt, StatusSadrzaja.Nacrt)]
    [InlineData(StatusSadrzaja.Objavljen, StatusSadrzaja.Objavljen)]
    [InlineData(StatusSadrzaja.Zakljucan, StatusSadrzaja.Nacrt)]
    [InlineData(StatusSadrzaja.Zakljucan, StatusSadrzaja.Zakljucan)]
    public void Nedozvoljeni_prelazi_bacaju(StatusSadrzaja iz, StatusSadrzaja u)
    {
        var s = new SedmicniSadrzaj { Status = iz };
        var ex = Assert.Throws<ConflictException>(() => s.Predji(u));
        Assert.Equal("nedozvoljen_prelaz_sadrzaja", ex.Code);
        Assert.Equal(iz, s.Status);
    }
}
