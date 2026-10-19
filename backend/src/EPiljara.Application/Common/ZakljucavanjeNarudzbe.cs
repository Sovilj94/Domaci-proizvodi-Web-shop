using EPiljara.Application.Common.Interfaces;

namespace EPiljara.Application.Common;

/// <summary>
/// B1/B2: dvije istovremene narudžbe istog kupca su mogle potrošiti iste poene, a dvije narudžbe
/// sa istim kodom preći njegov limit za jedan — obje čitaju stanje prije nego što ijedna upiše.
/// Redovi kupca i koda se zaključavaju u transakciji koja traje do upisa narudžbe, pa druga
/// narudžba čeka i čita već umanjeno stanje. SQL zaključavanja je u Infrastructure
/// (`AppDbContext.ZakljucajZaNarudzbuAsync`); baze bez `FOR UPDATE` (SQLite u testovima) ga preskaču.
/// </summary>
public static class ZakljucavanjeNarudzbe
{
    public static bool Podrzano(IAppDbContext db) => db.PodrzavaZakljucavanje;

    /// <summary>Zaključava red kupca (poeni) i red koda (limit). Mora se zvati unutar transakcije.</summary>
    public static Task ZakljucajAsync(IAppDbContext db, Guid? kupacId, string? promoKod, CancellationToken ct) =>
        db.PodrzavaZakljucavanje
            ? db.ZakljucajZaNarudzbuAsync(kupacId, string.IsNullOrWhiteSpace(promoKod) ? null : promoKod.Trim().ToUpperInvariant(), ct)
            : Task.CompletedTask;
}
