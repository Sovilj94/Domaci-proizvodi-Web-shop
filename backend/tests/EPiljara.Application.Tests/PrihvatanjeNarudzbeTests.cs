using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba se više ne odobrava ručno: čim je napravljena, prihvaćena je (Confirmed) i otvorena
/// za izmjene do roka. Oba mjesta koja prave Order (CreateOrder i potvrda gosta) moraju to raditi.
/// </summary>
public class PrihvatanjeNarudzbeTests
{
    [Fact]
    public async Task Narudzba_prijavljenog_kupca_je_odmah_prihvacena()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);

        var dto = await t.PosaljiObicanArtikalAsync();

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.SingleAsync(o => o.Id == dto.Id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.ZakljucanaU);
    }

    [Fact]
    public async Task Potvrdjena_gostujuca_narudzba_je_odmah_prihvacena()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var token = await t.ZahtjevGostaAsync(t.KrompirId);

        var dto = await t.PotvrdiGostaAsync(token);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.SingleAsync(o => o.Id == dto.Id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Null(order.ZakljucanaU);
    }

    [Fact]
    public async Task Prihvatanje_ne_salje_mejl_o_potvrdi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);

        await t.PosaljiObicanArtikalAsync();

        // Samo postojeća potvrda narudžbe — nikakav poseban „Narudžba je potvrđena" mejl.
        Assert.Single(t.Email.Narudzbe);
        Assert.DoesNotContain(t.Email.Statusi, s => s.Status == OrderStatus.Confirmed);
    }
}
