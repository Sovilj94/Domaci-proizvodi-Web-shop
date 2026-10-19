using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Common;

/// <summary>
/// Upis promjene statusa narudžbe (B3). Dva istovremena zahtjeva za istu narudžbu udare u
/// jedinstvene indekse (poeni po narudžbi, nagrada za preporuku) — drugi zahtjev dobija 409
/// sa porukom da osvježi, umjesto 500. Duple poene i dalje sprječava baza.
/// </summary>
public static class IstovremenaPromjena
{
    public static async Task SacuvajAsync(IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "Narudžba je u međuvremenu promijenjena. Osvježi stranicu i pokušaj ponovo.",
                "istovremena_promjena");
        }
    }
}
