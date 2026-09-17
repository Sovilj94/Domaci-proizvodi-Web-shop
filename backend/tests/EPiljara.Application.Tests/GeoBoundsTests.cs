using EPiljara.Application.Common;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class GeoBoundsTests
{
    [Fact]
    public void Tacka_u_Banjoj_Luci_je_unutar_granica()
    {
        GeoBounds.Contains(44.7722, 17.1910).Should().BeTrue();
    }

    [Fact]
    public void Zamijenjena_sirina_i_duzina_padaju_van_granica()
    {
        // Ovo je razlog zašto opseg cijelog globusa nije dovoljan: 17/44 je
        // sintaksno ispravna tačka, ali je u Iraku.
        GeoBounds.Contains(17.1910, 44.7722).Should().BeFalse();
    }
}
