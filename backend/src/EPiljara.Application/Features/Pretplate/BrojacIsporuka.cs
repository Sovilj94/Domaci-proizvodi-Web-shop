using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Jedino mjesto koje diže <see cref="Pretplata.BrojIsporuka"/>.
///
/// Postoji zato što status narudžbe u <see cref="OrderStatus.Delivered"/> prelazi na DVA
/// mjesta: admin ga mijenja padajućim spiskom (`AdminOrders`), a vozač zatvaranjem stanice
/// (`DriverFeature`). Prepisan brojač na oba mjesta bi se razišao prvom izmjenom pravila, a
/// razlika bi se vidjela tek kao popust koji je stigao prerano ili nikad. Isti razlog zbog
/// kojeg `SnapshotKorpice` postoji kao pomoćnik, a ne kao kopiran blok.
///
/// Popust se zarađuje ISPORUKAMA, ne narudžbama: brojač se zato ne diže pri nastanku
/// narudžbe nego tek kad je kupac robu stvarno dobio.
/// </summary>
public static class BrojacIsporuka
{
    /// <summary>
    /// Zove se PRIJE <c>SaveChangesAsync</c> onoga ko mijenja status, da izmjena statusa i
    /// brojač odu u isti upis. Ne radi ništa ako narudžba nije pretplatna, ako novi status
    /// nije isporuka, ili ako je narudžba već bila isporučena — ponovno postavljanje istog
    /// statusa ne smije brojati drugi put.
    /// </summary>
    public static async Task ZabiljeziAsync(
        IAppDbContext db, Order order, OrderStatus noviStatus, CancellationToken ct)
    {
        if (order.PretplataId is null) return;
        if (noviStatus != OrderStatus.Delivered) return;
        if (order.Status == OrderStatus.Delivered) return;

        var pretplata = await db.Pretplate
            .FirstOrDefaultAsync(p => p.Id == order.PretplataId, ct);
        // Pretplata se ne briše (otkaz je status), pa je null ovdje pokvaren red, ne scenarij.
        // Narudžba se svejedno mora isporučiti — brojač nije razlog da vozač ne zatvori stanicu.
        if (pretplata is null) return;

        pretplata.BrojIsporuka++;
    }
}
