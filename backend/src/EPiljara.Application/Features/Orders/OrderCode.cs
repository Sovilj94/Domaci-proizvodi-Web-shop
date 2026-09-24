using EPiljara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Orders;

/// <summary>
/// Broj narudžbe u obliku EP-yyMM-NNNN. Dijele ga obično naručivanje i potvrda
/// gosta — dvije kopije istog brojača bi prije ili kasnije dale isti broj.
/// </summary>
public static class OrderCode
{
    public static async Task<string> GenerateAsync(IAppDbContext db, CancellationToken ct)
    {
        var count = await db.Orders.CountAsync(ct);
        var now = DateTime.UtcNow;
        return $"EP-{now:yyMM}-{count + 41:D4}";
    }
}
