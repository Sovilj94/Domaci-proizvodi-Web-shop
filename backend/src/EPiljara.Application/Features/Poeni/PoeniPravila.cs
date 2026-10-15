using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Poeni;

/// <summary>
/// Jedino mjesto koje piše u knjigu poena pri promjeni statusa narudžbe. Status se mijenja
/// na dva mjesta (admin panel i vozačeva aplikacija), pa pravila žive ovdje a ne u kopijama —
/// isti razlog zbog kojeg postoji <c>BrojacIsporuka</c>.
/// </summary>
public static class PoeniPravila
{
    public static async Task<int> StanjeAsync(IAppDbContext db, Guid userId, CancellationToken ct) =>
        await db.PoeniStavke.Where(s => s.UserId == userId).SumAsync(s => s.Iznos, ct);

    public static (int Poena, decimal Popust) ZaTrosenje(int stanje, decimal robaPoslijePromo, PoeniOpcije o)
    {
        if (stanje <= 0 || robaPoslijePromo <= 0) return (0, 0m);
        // Cijele KM: kupac vidi „−12 KM", ne „−12,35 KM", a poeni se troše u okruglim blokovima.
        var popust = Math.Floor(Math.Min(stanje * o.VrijednostPoenaKM, robaPoslijePromo));
        // VrijednostPoenaKM mora dijeliti 1 KM bez ostatka (0.05, 0.1, 0.5...); inače poeni i KM
        // ne idu u cijelim blokovima. Round štiti samo od decimalnog šuma, ne od lošeg podešavanja.
        var poena = (int)Math.Round(popust / o.VrijednostPoenaKM);
        return (poena, popust);
    }

    public static int ZaradaZaNarudzbu(Order order, PoeniOpcije o) =>
        (int)Math.Floor(Math.Max(0, order.Subtotal - order.Popust - order.PoeniPopust)) * o.PoenaPoKM;

    /// <summary>
    /// Zove se PRIJE <c>SaveChangesAsync</c> onoga ko mijenja status, sa još STARIM statusom
    /// na narudžbi. Samo dodaje stavke; upis radi pozivalac. Duplikati se hvataju upitom
    /// jer ponovljen zahtjev ne smije dvaput nagraditi ni dvaput vratiti poene.
    /// </summary>
    public static async Task ZabiljeziPromjenuStatusaAsync(
        IAppDbContext db, Order order, OrderStatus novi, PoeniOpcije o, CancellationToken ct)
    {
        if (order.Status == novi) return;
        if (order.CustomerId is not Guid kupacId) return; // gost nema nalog, pa ni poene

        if (novi == OrderStatus.Delivered)
        {
            // Admin i vozač unose telefonske narudžbe pod SVOJIM nalogom (vidi komentar o CustomerId
            // u CreateOrder.cs), pa bi zarada i bonus za preporuku završili kod osoblja, ne kod kupca.
            // Zato se zarađuje samo na nalogu kupca. Povrat ispod ostaje bezuslovan: vraća ono što
            // je stvarno potrošeno.
            if (!await db.Users.AnyAsync(u => u.Id == kupacId && u.Role == UserRole.Customer, ct)) return;

            var vecIsporuka = await db.PoeniStavke
                .AnyAsync(s => s.OrderId == order.Id && s.Vrsta == VrstaPoena.Isporuka, ct);
            if (vecIsporuka) return;

            var zarada = ZaradaZaNarudzbu(order, o);
            if (zarada > 0)
                db.PoeniStavke.Add(new PoeniStavka
                {
                    UserId = kupacId, Iznos = zarada, Vrsta = VrstaPoena.Isporuka, OrderId = order.Id,
                });

            await NagradiPreporukuAsync(db, kupacId, order.Id, o, ct);
        }
        else if (novi == OrderStatus.Cancelled && order.IskoristeniPoeni > 0)
        {
            var vecPovrat = await db.PoeniStavke
                .AnyAsync(s => s.OrderId == order.Id && s.Vrsta == VrstaPoena.Povrat, ct);
            if (vecPovrat) return;

            db.PoeniStavke.Add(new PoeniStavka
            {
                UserId = kupacId, Iznos = order.IskoristeniPoeni, Vrsta = VrstaPoena.Povrat, OrderId = order.Id,
            });
        }
    }

    private static async Task NagradiPreporukuAsync(
        IAppDbContext db, Guid kupacId, Guid orderId, PoeniOpcije o, CancellationToken ct)
    {
        var preporucioId = await db.Users
            .Where(u => u.Id == kupacId).Select(u => u.PreporucioId).FirstOrDefaultAsync(ct);
        if (preporucioId is not Guid preporucilac) return;

        // Nagrada se daje samo jednom, za prvu isporuku.
        var vecNagradjen = await db.PoeniStavke
            .AnyAsync(s => s.UserId == kupacId && s.Vrsta == VrstaPoena.PreporukaNovi, ct);
        if (vecNagradjen) return;

        // Preporučilac može biti obrisan; tada ne nagrađujemo nijednu stranu.
        if (!await db.Users.AnyAsync(u => u.Id == preporucilac, ct)) return;

        db.PoeniStavke.Add(new PoeniStavka
        {
            UserId = kupacId, Iznos = o.NagradaPreporuka, Vrsta = VrstaPoena.PreporukaNovi,
            OrderId = orderId, PreporuceniId = kupacId,
        });
        db.PoeniStavke.Add(new PoeniStavka
        {
            UserId = preporucilac, Iznos = o.NagradaPreporuka, Vrsta = VrstaPoena.PreporukaPreporucio,
            OrderId = orderId, PreporuceniId = kupacId,
        });
    }
}
