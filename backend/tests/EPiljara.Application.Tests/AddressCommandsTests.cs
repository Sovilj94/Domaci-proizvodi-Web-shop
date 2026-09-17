using EPiljara.Application.Features.Account;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class AddressCommandsTests
{
    private static User NapraviKupca(TestDb t, string email)
    {
        var u = new User { Name = "Kupac", Email = email, PasswordHash = "x" };
        t.Db.Users.Add(u);
        t.Db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Prva_adresa_je_glavna_sama_od_sebe()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t, "a@test.local");
        var handler = new CreateAddressHandler(t.Db, new FakeCurrentUser(u.Id));

        var rezultat = await handler.Handle(
            new CreateAddressCommand(null, "Prva 1", null, null, null), default);

        rezultat.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Postavljanje_glavne_gasi_prethodnu()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t, "b@test.local");
        var create = new CreateAddressHandler(t.Db, new FakeCurrentUser(u.Id));
        var prva = await create.Handle(new CreateAddressCommand(null, "Prva 1", null, null, null), default);
        var druga = await create.Handle(new CreateAddressCommand(null, "Druga 2", null, null, null), default);

        var setDefault = new SetDefaultAddressHandler(t.Db, new FakeCurrentUser(u.Id));
        await setDefault.Handle(new SetDefaultAddressCommand(druga.Id), default);

        var sve = t.Db.Addresses.Where(a => a.UserId == u.Id).ToList();
        sve.Count(a => a.IsDefault).Should().Be(1, "nikad ne smiju postojati dvije glavne");
        sve.Single(a => a.IsDefault).Id.Should().Be(druga.Id);
        sve.Single(a => a.Id == prva.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Brisanje_glavne_prebacuje_oznaku_na_preostalu()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t, "c@test.local");
        var create = new CreateAddressHandler(t.Db, new FakeCurrentUser(u.Id));
        var prva = await create.Handle(new CreateAddressCommand(null, "Prva 1", null, null, null), default);
        await create.Handle(new CreateAddressCommand(null, "Druga 2", null, null, null), default);

        var delete = new DeleteAddressHandler(t.Db, new FakeCurrentUser(u.Id));
        await delete.Handle(new DeleteAddressCommand(prva.Id), default);

        var sve = t.Db.Addresses.Where(a => a.UserId == u.Id).ToList();
        sve.Should().HaveCount(1);
        sve.Single().IsDefault.Should().BeTrue("kupac ne smije ostati bez glavne adrese");
    }

    // Redoslijed UPDATE-a unutar jednog SaveChanges zavisi od (nasumičnih) GUID ključeva,
    // pa je greška sa filtriranim unique indeksom ispadala samo ponekad. Mnogo prebacivanja
    // i brisanja na istom korisniku je čini praktično sigurnom ako se vrati.
    [Fact]
    public async Task Glavna_adresa_se_moze_prebacivati_i_brisati_bez_sudara_indeksa()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t, "e@test.local");
        var kupac = new FakeCurrentUser(u.Id);
        var create = new CreateAddressHandler(t.Db, kupac);
        var setDefault = new SetDefaultAddressHandler(t.Db, kupac);
        var delete = new DeleteAddressHandler(t.Db, kupac);

        var ids = new List<Guid>();
        for (var i = 1; i <= CreateAddressHandler.MaxAdresa; i++)
            ids.Add((await create.Handle(new CreateAddressCommand(null, $"Ulica {i}", null, null, null), default)).Id);

        for (var krug = 0; krug < 3; krug++)
            foreach (var id in ids)
                await setDefault.Handle(new SetDefaultAddressCommand(id), default);

        // Brisanje glavne svaki put prebacuje oznaku na neku od preostalih.
        while (ids.Count > 1)
        {
            var glavna = t.Db.Addresses.Single(a => a.UserId == u.Id && a.IsDefault).Id;
            await delete.Handle(new DeleteAddressCommand(glavna), default);
            ids.Remove(glavna);
            t.Db.Addresses.Count(a => a.UserId == u.Id && a.IsDefault).Should().Be(1);
        }
    }

    [Fact]
    public async Task Tudja_adresa_se_ne_moze_obrisati()
    {
        using var t = new TestDb();
        var vlasnik = NapraviKupca(t, "vlasnik@test.local");
        var napadac = NapraviKupca(t, "napadac@test.local");

        var create = new CreateAddressHandler(t.Db, new FakeCurrentUser(vlasnik.Id));
        var adresa = await create.Handle(new CreateAddressCommand(null, "Moja 1", null, null, null), default);

        var delete = new DeleteAddressHandler(t.Db, new FakeCurrentUser(napadac.Id));
        var cin = () => delete.Handle(new DeleteAddressCommand(adresa.Id), default);

        await cin.Should().ThrowAsync<ForbiddenException>();
        t.Db.Addresses.Should().ContainSingle(a => a.Id == adresa.Id);
    }

    [Fact]
    public async Task Jedanaesta_adresa_se_odbija()
    {
        using var t = new TestDb();
        var u = NapraviKupca(t, "d@test.local");
        var create = new CreateAddressHandler(t.Db, new FakeCurrentUser(u.Id));

        for (var i = 1; i <= CreateAddressHandler.MaxAdresa; i++)
            await create.Handle(new CreateAddressCommand(null, $"Ulica {i}", null, null, null), default);

        var cin = () => create.Handle(new CreateAddressCommand(null, "Preko granice", null, null, null), default);

        (await cin.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("address_limit");
    }
}
