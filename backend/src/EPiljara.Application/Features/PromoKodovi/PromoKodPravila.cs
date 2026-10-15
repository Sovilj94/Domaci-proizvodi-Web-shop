using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.PromoKodovi;

/// <summary>Ko primjenjuje kod. Gost bez unesenog e-maila je (null, null).</summary>
public record KupacZaPromo(Guid? CustomerId, string? Email);

/// <summary>
/// Jedino mjesto sa pravilima promo koda. Zovu ga provjera na checkoutu, oba načina
/// naručivanja i potvrda gosta — pravilo ponovljeno na četiri mjesta bi se razišlo.
/// </summary>
public static class PromoKodPravila
{
    public const string Greska = "promo_kod_nevazeci";

    public static string Normalizuj(string kod) => (kod ?? "").Trim().ToUpperInvariant();

    /// <summary>Popust na robu; nikad veći od nje (O1). AwayFromZero jer kupac očekuje školsko zaokruživanje.</summary>
    public static decimal Popust(PromoKod kod, decimal subtotal)
    {
        var p = kod.Tip == TipPopusta.Procenat
            ? Math.Round(subtotal * kod.Vrijednost / 100m, 2, MidpointRounding.AwayFromZero)
            : kod.Vrijednost;
        return Math.Min(p, subtotal);
    }

    public static async Task<PromoKod> ProvjeriAsync(
        IAppDbContext db, string unos, decimal subtotal, KupacZaPromo kupac, DateOnly danas,
        CancellationToken ct)
    {
        var tekst = Normalizuj(unos);
        var kod = await db.PromoKodovi.FirstOrDefaultAsync(k => k.Kod == tekst, ct);
        // Ugašen i nepostojeći kod daju istu poruku — ne otkriva se da je ugašeni postojao (O12).
        if (kod is null || !kod.Aktivan)
            throw new DomainException("Promo kod ne postoji ili nije aktivan.", Greska);
        if (kod.VaziOd is { } od && danas < od)
            throw new DomainException("Promo kod još ne važi.", Greska);
        if (kod.VaziDo is { } kraj && danas > kraj)
            throw new DomainException("Promo kod je istekao.", Greska);
        if (kod.MinIznos is { } min && subtotal < min)
            throw new DomainException($"Promo kod važi za narudžbe od {min:0.##} KM.", Greska);

        // Otkazane narudžbe ne troše kod (O7).
        var aktivne = db.Orders.Where(o => o.Status != OrderStatus.Cancelled);

        if (kod.MaxKoristenja is { } max &&
            await aktivne.CountAsync(o => o.PromoKodId == kod.Id, ct) >= max)
            throw new DomainException("Promo kod je iskorišten.", Greska);

        var email = string.IsNullOrWhiteSpace(kupac.Email) ? null : kupac.Email.Trim().ToLower();
        if (kupac.CustomerId is null && email is null)
            return kod; // nema po čemu prepoznati kupca; konačna provjera pri slanju ima e-mail

        var odKupca = aktivne.Where(o =>
            (kupac.CustomerId != null && o.CustomerId == kupac.CustomerId) ||
            (email != null && o.CustomerEmail != null && o.CustomerEmail.ToLower() == email));

        if (kod.JednomPoKupcu && await odKupca.AnyAsync(o => o.PromoKodId == kod.Id, ct))
            throw new DomainException("Promo kod ste već iskoristili.", Greska);
        if (kod.SamoNoviKupci && await odKupca.AnyAsync(ct))
            throw new DomainException("Promo kod važi samo za prvu narudžbu.", Greska);

        return kod;
    }
}
