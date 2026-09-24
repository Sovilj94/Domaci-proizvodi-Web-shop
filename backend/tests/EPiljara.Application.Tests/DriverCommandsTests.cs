using EPiljara.Application.Features.Admin;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class DriverCommandsTests
{
    private static DriverInput Unos(
        string name = "Mladen Vozač",
        string phone = "+387 65 900 100",
        string? vozilo = "Kombi 1",
        string? registracija = "BL-123-A",
        bool aktivan = true) => new(name, phone, vozilo, registracija, aktivan);

    private static Driver NapraviVozaca(TestDb t, string name = "Postojeći")
    {
        var d = new Driver { Name = name, Phone = "+387 65 111 222" };
        t.Db.Drivers.Add(d);
        t.Db.SaveChanges();
        return d;
    }

    [Fact]
    public async Task Kreiranje_upisuje_vozaca()
    {
        using var t = new TestDb();
        var handler = new CreateDriverHandler(t.Db);

        var rezultat = await handler.Handle(new CreateDriverCommand(Unos()), default);

        rezultat.Name.Should().Be("Mladen Vozač");
        rezultat.Vozilo.Should().Be("Kombi 1");
        rezultat.Registracija.Should().Be("BL-123-A");
        rezultat.Aktivan.Should().BeTrue();
        t.Db.Drivers.Should().ContainSingle(d => d.Id == rezultat.Id);
    }

    [Fact]
    public async Task Kreiranje_trimuje_ime_i_telefon()
    {
        using var t = new TestDb();
        var handler = new CreateDriverHandler(t.Db);

        var rezultat = await handler.Handle(
            new CreateDriverCommand(Unos(name: "  Petar  ", phone: "  065 1  ")), default);

        rezultat.Name.Should().Be("Petar");
        rezultat.Phone.Should().Be("065 1");
    }

    [Fact]
    public async Task Izmjena_mijenja_podatke_vozaca()
    {
        using var t = new TestDb();
        var vozac = NapraviVozaca(t);
        var handler = new UpdateDriverHandler(t.Db);

        var rezultat = await handler.Handle(
            new UpdateDriverCommand(vozac.Id, Unos(name: "Novo ime", vozilo: "Kombi 2", aktivan: false)),
            default);

        rezultat.Name.Should().Be("Novo ime");
        rezultat.Vozilo.Should().Be("Kombi 2");
        rezultat.Aktivan.Should().BeFalse();
    }

    [Fact]
    public async Task Izmjena_nepostojeceg_vozaca_baca_not_found()
    {
        using var t = new TestDb();
        var handler = new UpdateDriverHandler(t.Db);

        var radnja = () => handler.Handle(new UpdateDriverCommand(Guid.NewGuid(), Unos()), default);

        (await radnja.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Brisanje_uklanja_vozaca_bez_ruta()
    {
        using var t = new TestDb();
        var vozac = NapraviVozaca(t);
        var handler = new DeleteDriverHandler(t.Db);

        await handler.Handle(new DeleteDriverCommand(vozac.Id), default);

        t.Db.Drivers.Should().NotContain(d => d.Id == vozac.Id);
    }

    [Fact]
    public async Task Brisanje_nepostojeceg_vozaca_baca_not_found()
    {
        using var t = new TestDb();
        var handler = new DeleteDriverHandler(t.Db);

        var radnja = () => handler.Handle(new DeleteDriverCommand(Guid.NewGuid()), default);

        await radnja.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Brisanje_vozaca_sa_rutom_baca_konflikt()
    {
        using var t = new TestDb();
        var vozac = NapraviVozaca(t);
        t.Db.DeliveryRoutes.Add(new DeliveryRoute
        {
            DriverId = vozac.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Naziv = "Ruta 1",
        });
        t.Db.SaveChanges();
        var handler = new DeleteDriverHandler(t.Db);

        var radnja = () => handler.Handle(new DeleteDriverCommand(vozac.Id), default);

        // Istorija dana mora ostati netaknuta — vozač se penzioniše preko Aktivan=false.
        (await radnja.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("driver_in_use");
        t.Db.Drivers.Should().Contain(d => d.Id == vozac.Id);
    }

    [Fact]
    public void Validator_trazi_ime_i_telefon()
    {
        var validator = new DriverInputValidator();

        var rezultat = validator.Validate(Unos(name: "  ", phone: ""));

        rezultat.Errors.Select(e => e.PropertyName).Should().Contain(["Name", "Phone"]);
    }
}
