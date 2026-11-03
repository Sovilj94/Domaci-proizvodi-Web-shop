using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>Učitavanje za analitiku. Sve read-only; račun je u PoslovanjeRacun.</summary>
public static class PoslovanjePodaci
{
    /// <summary>Narudžbe (svih statusa) tačno datih sedmica, sa stavkama, zonom i ocjenom.</summary>
    public static async Task<List<Order>> NarudzbeAsync(
        IAppDbContext db, IReadOnlyCollection<(int Godina, int Sedmica)> sedmice, CancellationToken ct)
    {
        if (sedmice.Count == 0) return [];
        var godine = sedmice.Select(s => s.Godina).Distinct().ToList();
        var brojevi = sedmice.Select(s => s.Sedmica).Distinct().ToList();
        var skup = sedmice.ToHashSet();
        var narudzbe = await db.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.KorpicaStavke)
            .Include(o => o.DeliveryZone)
            .Include(o => o.Ocjena)
            .Where(o => godine.Contains(o.Godina) && brojevi.Contains(o.Sedmica))
            .ToListAsync(ct);
        return narudzbe.Where(o => skup.Contains((o.Godina, o.Sedmica))).ToList();
    }

    /// <summary>Cijeli katalog, i obrisani artikli — stare narudžbe ih i dalje nose.</summary>
    public static Task<Dictionary<Guid, Product>> KatalogAsync(IAppDbContext db, CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .ToDictionaryAsync(p => p.Id, ct);
}
