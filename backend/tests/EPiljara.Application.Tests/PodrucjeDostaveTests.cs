using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Geocoding;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EPiljara.Application.Tests;

public class PodrucjeDostaveTests
{
    private static GeocodeHit Pogodak(string? grad, string? opstina) =>
        new(44.77, 17.19, "x", []) { Grad = grad, Opstina = opstina };

    [Fact]
    public void Adresa_u_Banjoj_Luci_je_u_podrucju()
    {
        PodrucjeDostave.Pripada(Pogodak("Banja Luka", "Grad Banja Luka"), "Banja Luka").Should().BeTrue();
    }

    [Fact]
    public void Selo_na_teritoriji_grada_je_u_podrucju_po_opstini()
    {
        PodrucjeDostave.Pripada(Pogodak("Bistrica", "Grad Banja Luka"), "Banja Luka").Should().BeTrue();
    }

    [Fact]
    public void Drugi_grad_nije_u_podrucju()
    {
        PodrucjeDostave.Pripada(Pogodak("Laktaši", "Opština Laktaši"), "Banja Luka").Should().BeFalse();
        PodrucjeDostave.Pripada(Pogodak("Livno", "Grad Livno"), "Banja Luka").Should().BeFalse();
    }

    [Fact]
    public void Cirilica_i_kvacice_se_ne_racunaju()
    {
        PodrucjeDostave.Pripada(Pogodak(null, "Град Бања Лука"), "banja luka").Should().BeTrue();
    }

    [Fact]
    public void Bez_grada_i_opstine_nema_odluke()
    {
        PodrucjeDostave.Pripada(Pogodak(null, null), "Banja Luka").Should().BeNull();
    }

    [Fact]
    public void Bez_podesenog_grada_dostave_sve_prolazi()
    {
        PodrucjeDostave.Pripada(Pogodak("Livno", null), "").Should().BeTrue();
    }

    private sealed class DvaGrada : IAddressSuggester
    {
        public Task<IReadOnlyList<GeocodeHit>> SuggestAsync(string query, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GeocodeHit>>(
            [
                new GeocodeHit(43.8, 16.98, "Gundulićeva 10, Livno", []) { Ulica = "Gundulićeva", Grad = "Livno", Opstina = "Grad Livno" },
                new GeocodeHit(44.77, 17.2, "Gundulićeva 10, Banja Luka", []) { Ulica = "Gundulićeva", Grad = "Banja Luka", Opstina = "Grad Banja Luka" },
            ]);
    }

    [Fact]
    public async Task Prijedlozi_iz_grada_dostave_idu_prvi_a_ostali_su_oznaceni()
    {
        var rezultat = await new SuggestAddressHandler(
                new DvaGrada(), Options.Create(new DostavaOpcije { GradDostave = "Banja Luka" }))
            .Handle(new SuggestAddressQuery("Gundulićeva 10"), default);

        rezultat.Select(r => (r.City, r.UPodrucjuDostave))
            .Should().Equal(("Banja Luka", true), ("Livno", false));
        rezultat.Should().OnlyContain(r => r.GradDostave == "Banja Luka");
    }
}
