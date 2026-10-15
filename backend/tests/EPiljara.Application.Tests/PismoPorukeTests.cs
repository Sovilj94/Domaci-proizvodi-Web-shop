using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using EPiljara.Infrastructure.Services;
using FluentAssertions;

namespace EPiljara.Application.Tests;

/// <summary>Mejl i Viber idu na pismu narudžbe; šabloni ostaju latinični, preslovljava se izlaz.</summary>
public class PismoPorukeTests
{
    private static Order Narudzba(Pismo pismo) => new()
    {
        Code = "EP-2610-0054", CustomerName = "Marko Marković", DeliveryAddress = "Kralja Petra 12",
        Subtotal = 40m, DeliveryFee = 3m, TotalAmount = 43m, Pismo = pismo,
        Items = [new OrderItem { ProductName = "Babura paprika", Quantity = 10m, UnitPrice = 4m, Unit = Unit.Kg }],
    };

    private const string Track = "https://epiljara.ba/narudzba/abc123";

    [Fact]
    public void Mejl_narudzbe_na_cirilici_cuva_kod_i_link()
    {
        var (naslov, html) = PismoPoruke.Primijeni(
            Pismo.Cirilica, "Potvrda narudžbe EP-2610-0054 — E piljara",
            OrderEmailTemplate.Build(Narudzba(Pismo.Cirilica), Track));

        naslov.Should().Be("Потврда наруџбе EP-2610-0054 — E piljara");
        html.Should().Contain("Хвала").And.NotContain("Hvala");
        html.Should().Contain("Бабура паприка");
        html.Should().Contain("EP-2610-0054");
        html.Should().Contain($"href=\"{Track}\"");
    }

    [Fact]
    public void Latinica_vraca_ulaz_nepromijenjen()
    {
        var html = OrderEmailTemplate.Build(Narudzba(Pismo.Latinica), Track);

        var (naslov, izlaz) = PismoPoruke.Primijeni(Pismo.Latinica, "Potvrda narudžbe", html);

        naslov.Should().Be("Potvrda narudžbe");
        izlaz.Should().BeSameAs(html);
    }

    [Fact]
    public void Viber_na_cirilici()
    {
        var tekst = PismoPoruke.Tekst(Pismo.Cirilica, ViberMessage.Build(Narudzba(Pismo.Cirilica)));

        tekst.Should().Contain("EP-2610-0054").And.Contain("Хвала").And.NotContain("Hvala");
        tekst.Should().StartWith("E piljara");
    }
}
