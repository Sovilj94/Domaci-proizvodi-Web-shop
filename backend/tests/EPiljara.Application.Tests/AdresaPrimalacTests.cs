using EPiljara.Application.Features.Account;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>Adresa u adresaru može nositi primaoca (narudžba za nekog drugog).</summary>
public class AdresaPrimalacTests
{
    private static User NapraviKupca(TestDb t)
    {
        var u = new User { Name = "Kupac", Email = "p@test.local", PasswordHash = "x" };
        t.Db.Users.Add(u);
        t.Db.DeliveryZones.Add(new DeliveryZone { Name = "Centar", IsActive = true, Granica = TestNaselje.Granica });
        t.Db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Adresa_pamti_primaoca()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t);

        var dto = await new CreateAddressHandler(t.Db, new FakeCurrentUser(u.Id)).Handle(
            new CreateAddressCommand("Mama i tata", "Kozaračka 5", TestNaselje.Lat, TestNaselje.Lon, null,
                "Petar Petrović", "+387 65 111 222"), default);

        dto.PrimalacIme.Should().Be("Petar Petrović");
        dto.PrimalacTelefon.Should().Be("+387 65 111 222");
        t.Db.ChangeTracker.Clear();
        var red = await t.Db.Addresses.AsNoTracking().FirstAsync(a => a.Id == dto.Id);
        red.PrimalacIme.Should().Be("Petar Petrović");
        red.PrimalacTelefon.Should().Be("+387 65 111 222");
    }

    [Fact]
    public void Primalac_bez_telefona_je_odbijen()
    {
        var r = new CreateAddressValidator().Validate(
            new CreateAddressCommand("x", "Ulica 1", TestNaselje.Lat, TestNaselje.Lon, null, "Petar", null));

        r.Errors.Should().Contain(e => e.PropertyName == "primalac");
    }

    [Fact]
    public async Task Izmjena_brise_primaoca_kad_su_polja_prazna()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t);
        var cu = new FakeCurrentUser(u.Id);
        var dto = await new CreateAddressHandler(t.Db, cu).Handle(
            new CreateAddressCommand(null, "Kozaračka 5", TestNaselje.Lat, TestNaselje.Lon, null,
                "Petar Petrović", "+387 65 111 222"), default);

        var izmijenjena = await new UpdateAddressHandler(t.Db, cu).Handle(
            new UpdateAddressCommand(dto.Id, null, "Kozaračka 5", TestNaselje.Lat, TestNaselje.Lon, null, "", ""), default);

        izmijenjena.PrimalacIme.Should().BeNull();
        izmijenjena.PrimalacTelefon.Should().BeNull();
    }
}
