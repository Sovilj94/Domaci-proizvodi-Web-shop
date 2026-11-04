using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
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

    /// <summary>Za svakog kupca: indeks mjeseca (PoslovanjeKalendar.Indeks) njegove prve isporučene narudžbe.</summary>
    public static async Task<Dictionary<string, int>> PrviMjeseciAsync(IAppDbContext db, DayOfWeek dan, CancellationToken ct)
    {
        var redovi = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => new { o.Id, o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Godina, o.Sedmica })
            .ToListAsync(ct);
        return redovi
            .GroupBy(r => PoslovanjeRacun.KljucKupca(r.CustomerId, r.CustomerEmail, r.CustomerPhone, r.Id))
            .ToDictionary(g => g.Key, g => g.Min(r =>
            {
                var (gd, m) = PoslovanjeKalendar.MjesecIsporuke(r.Godina, r.Sedmica, dan);
                return PoslovanjeKalendar.Indeks(gd, m);
            }));
    }

    /// <summary>Troškovi, ponavljajući, rute i cijene goriva za kalendarske dane [od, doIskljucivo).</summary>
    public static async Task<TroskoviPodaci> TroskoviAsync(IAppDbContext db, DateOnly od, DateOnly doIskljucivo, CancellationToken ct) =>
        new(
            await db.Troskovi.AsNoTracking().Include(t => t.Vrsta)
                .Where(t => t.Datum >= od && t.Datum < doIskljucivo).ToListAsync(ct),
            await db.PonavljajuciTroskovi.AsNoTracking().Include(t => t.Vrsta).ToListAsync(ct),
            await db.DeliveryRoutes.AsNoTracking().Include(r => r.Stops).ThenInclude(s => s.Order)
                .Where(r => r.Date >= od && r.Date < doIskljucivo).ToListAsync(ct),
            await db.CijeneGoriva.AsNoTracking().ToListAsync(ct),
            await db.DnevneVoznje.AsNoTracking().Where(v => v.Datum >= od && v.Datum < doIskljucivo).ToListAsync(ct));

    public static async Task<HashSet<Guid>> PreporuceniAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.Users.AsNoTracking().Where(u => u.PreporucioId != null).Select(u => u.Id).ToListAsync(ct)).ToHashSet();

    /// <summary>Cijeli katalog, i obrisani artikli — stare narudžbe ih i dalje nose.</summary>
    public static Task<Dictionary<Guid, Product>> KatalogAsync(IAppDbContext db, CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .ToDictionaryAsync(p => p.Id, ct);
}
