using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Dokazuje da `TestDb` stvarno enforsuje strane ključeve, a ne samo šemu —
/// tačno ono zbog čega brief zabranjuje EF-ov InMemory provider.
/// </summary>
public class TestDbTests
{
    [Fact]
    public void Upis_stavke_narudzbe_sa_nepostojecim_OrderId_baca_izuzetak()
    {
        using var testDb = new TestDb();

        testDb.Db.OrderItems.Add(new OrderItem
        {
            OrderId = Guid.NewGuid(), // ne postoji nijedna narudžba sa ovim Id-jem
            ProductId = Guid.NewGuid(),
            ProductName = "Med bagremov 900g",
            Unit = Unit.Kom,
            Quantity = 1,
            UnitPrice = 10,
        });

        var upis = () => testDb.Db.SaveChanges();

        upis.Should().Throw<DbUpdateException>();
    }
}
