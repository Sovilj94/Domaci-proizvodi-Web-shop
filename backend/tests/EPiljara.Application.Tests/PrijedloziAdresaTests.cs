using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Geocoding;
using EPiljara.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class PrijedloziAdresaTests
{
    private static string Feature(
        double lon, double lat, string countrycode, string osmKey,
        string? name = null, string? street = null, string? housenumber = null,
        string? city = null, string? district = null)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "Feature",
            geometry = new { type = "Point", coordinates = new[] { lon, lat } },
            properties = new { countrycode, osm_key = osmKey, name, street, housenumber, city, district },
        });

    private static string Kolekcija(params string[] features) =>
        $$"""{"type":"FeatureCollection","features":[{{string.Join(',', features)}}]}""";

    [Fact]
    public void Kuca_daje_ulicu_broj_i_grad()
    {
        var json = Kolekcija(Feature(17.2026, 44.7778, "BA", "building",
            street: "Gundulićeva", housenumber: "10", city: "Banja Luka", district: "Centar"));

        var hit = PhotonAddressSuggester.Procitaj(json).Should().ContainSingle().Subject;

        hit.Ulica.Should().Be("Gundulićeva");
        hit.Broj.Should().Be("10");
        hit.Grad.Should().Be("Banja Luka");
        hit.DisplayName.Should().Be("Gundulićeva 10, Banja Luka");
        hit.Lat.Should().Be(44.7778);
        hit.Lon.Should().Be(17.2026);
        hit.Localities.Should().Equal("Centar");
    }

    [Fact]
    public void Ista_ulica_iz_vise_OSM_komada_je_jedan_prijedlog()
    {
        var json = Kolekcija(
            Feature(17.19, 44.77, "BA", "highway", name: "Kralja Petra I Karađorđevića", city: "Banja Luka"),
            Feature(17.191, 44.771, "BA", "highway", name: "Kralja Petra I Karađorđevića", city: "Banja Luka"),
            Feature(17.192, 44.772, "BA", "highway", name: "Kralja Petra II", city: "Banja Luka"));

        PhotonAddressSuggester.Procitaj(json).Select(h => h.Ulica)
            .Should().Equal("Kralja Petra I Karađorđevića", "Kralja Petra II");
    }

    [Fact]
    public void Pogodak_izvan_BiH_se_odbacuje()
    {
        // Slakovci (HR) padaju u pravougaonik GeoBounds-a — zato se gleda i countrycode.
        var json = Kolekcija(
            Feature(18.95, 45.22, "HR", "building", street: "Ulica Ivana Gundulića", housenumber: "10", city: "Slakovci"),
            Feature(16.98, 43.81, "BA", "building", street: "Ivana Gundulića", housenumber: "10", city: "Livno"));

        PhotonAddressSuggester.Procitaj(json).Select(h => h.Grad).Should().Equal("Livno");
    }

    [Fact]
    public void Objekat_zadrzava_ime_ispred_adrese_a_naselje_bez_ulice_otpada()
    {
        var json = Kolekcija(
            Feature(17.18, 44.76, "BA", "amenity", name: "Pekara Mrvica", street: "Srpska", housenumber: "5", city: "Banja Luka"),
            Feature(17.1, 44.7, "BA", "place", name: "Lazarevo", city: "Banja Luka"));

        var hit = PhotonAddressSuggester.Procitaj(json).Should().ContainSingle().Subject;
        hit.DisplayName.Should().Be("Pekara Mrvica, Srpska 5, Banja Luka");
        hit.Ulica.Should().Be("Srpska");
    }

    [Fact]
    public void Najvise_pet_prijedloga()
    {
        var features = Enumerable.Range(1, 9)
            .Select(i => Feature(17.19, 44.77, "BA", "building", street: "Srpska", housenumber: $"{i}", city: "Banja Luka"))
            .ToArray();

        PhotonAddressSuggester.Procitaj(Kolekcija(features)).Should().HaveCount(5);
    }

    [Theory]
    [InlineData("Gundulićeva 10", "Gundulićeva", "10")]
    [InlineData("Kralja Petra I Karađorđevića 10a", "Kralja Petra I Karađorđevića", "10a")]
    [InlineData("Gundulićeva", "Gundulićeva", null)]
    [InlineData("10", "10", null)]
    public void Broj_na_kraju_upita_se_odvaja(string upit, string ulica, string? broj)
    {
        PhotonAddressSuggester.OdvojiBroj(upit).Should().Be((ulica, broj));
    }

    private static readonly Microsoft.Extensions.Options.IOptions<DostavaOpcije> BanjaLuka =
        Microsoft.Extensions.Options.Options.Create(new DostavaOpcije { GradDostave = "Banja Luka" });

    private sealed class BrojacPoziva : IAddressSuggester
    {
        public int Pozivi { get; private set; }

        public Task<IReadOnlyList<GeocodeHit>> SuggestAsync(string query, CancellationToken ct = default)
        {
            Pozivi++;
            return Task.FromResult<IReadOnlyList<GeocodeHit>>(
                [new GeocodeHit(44.77, 17.19, "Srpska 5, Banja Luka", []) { Ulica = "Srpska", Broj = "5" }]);
        }
    }

    [Fact]
    public async Task Kratak_upit_ne_izlazi_iz_sistema()
    {
        var servis = new BrojacPoziva();
        var rezultat = await new SuggestAddressHandler(servis, BanjaLuka).Handle(new SuggestAddressQuery(" ab "), default);

        rezultat.Should().BeEmpty();
        servis.Pozivi.Should().Be(0);
    }

    [Fact]
    public async Task Prijedlog_nosi_ulicu_i_broj_do_pregledaca()
    {
        var rezultat = await new SuggestAddressHandler(new BrojacPoziva(), BanjaLuka)
            .Handle(new SuggestAddressQuery("Srpska 5"), default);

        var dto = rezultat.Should().ContainSingle().Subject;
        dto.Street.Should().Be("Srpska");
        dto.HouseNumber.Should().Be("5");
    }
}
