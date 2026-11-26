using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Sastavlja i odmah objavljuje sadržaj korpica za sedmicu koju kupci sada naručuju
/// (<see cref="SedmicaHelper.SedmicaZaNarudzbu"/>) — vlasnik ne mora ništa pokretati (spec 2026-11-26,
/// odluka 1). Zove je <c>ZakljucavanjeSadrzajaService</c> svakih 15 minuta, odmah poslije zaključavanja,
/// pa se nova sedmica otvara čim stara zatvori. <paramref name="Sada"/> postoji radi testova.
/// </summary>
public record AutoObjaviSadrzajeCommand(DateTime? Sada = null) : IRequest<AutoObjavaRezultat>;

/// <param name="PrvaVocnaUSezoni">
/// U ovom prolazu je objavljena voćna korpica, a prethodna sedmica voćnog sadržaja nije imala —
/// znak da je sezona voća upravo krenula (obavještenje prijavljenima, Task 5).
/// </param>
public record AutoObjavaRezultat(int Godina, int Sedmica, int Objavljeno, bool PrvaVocnaUSezoni);

/// <summary>
/// Po korpici: objavljen/zaključan sadržaj se ne dira; nacrt sa stavkama se objavljuje kakav jeste
/// (admin ga je počeo); prazan nacrt ili nikakav se sastavlja generatorom pa objavljuje. Voće van sezone
/// se preskače. Jedna korpica koja pukne (obrisan artikal, pravila objave, trka dva prolaza) ostaje
/// nacrt i ne zaustavlja ostale — adminu to javlja postojeći alarm za neobjavljen sadržaj.
/// </summary>
public class AutoObjaviSadrzajeHandler(
    IAppDbContext db, IOptions<KorpiceOpcije> opcije, ILogger<AutoObjaviSadrzajeHandler> log)
    : IRequestHandler<AutoObjaviSadrzajeCommand, AutoObjavaRezultat>
{
    public async Task<AutoObjavaRezultat> Handle(AutoObjaviSadrzajeCommand c, CancellationToken ct)
    {
        var o = opcije.Value;
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, o, ct);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff, c.Sada);
        var mjesec = SezonaVoca.MjesecIsporuke(godina, sedmica, dan, vrijemeOd);

        var korpice = await db.Products
            .Where(p => p.Vrsta == VrstaProizvoda.Korpica && p.IsAvailable)
            .OrderBy(p => p.TipKorpice).ThenBy(p => p.Velicina)
            .Select(p => new { p.Id, p.Name, p.TipKorpice })
            .ToListAsync(ct);

        var postojeci = await db.SedmicniSadrzaji
            .Where(s => s.Godina == godina && s.Sedmica == sedmica)
            .Select(s => new { s.KorpicaId, s.Id, s.Status, Stavki = s.Stavke.Count })
            .ToListAsync(ct);

        var generator = new GenerisiSedmicniSadrzajHandler(db, opcije);
        var objava = new ObjaviSadrzajHandler(db, opcije);
        var objavljeno = 0;
        var vocnaObjavljena = false;

        foreach (var k in korpice)
        {
            if (SezonaVoca.VanSezone(o, k.TipKorpice, mjesec)) continue;

            var s = postojeci.FirstOrDefault(x => x.KorpicaId == k.Id);
            if (s is { Status: StatusSadrzaja.Objavljen or StatusSadrzaja.Zakljucan }) continue;

            try
            {
                var sadrzajId = s is { Stavki: > 0 } ? s.Id : await SastaviAsync(generator, k.Id, k.Name, godina, sedmica, ponovo: s is not null, ct);
                if (sadrzajId is null) continue;

                await objava.Handle(new ObjaviSadrzajCommand(sadrzajId.Value), ct);
                objavljeno++;
                if (k.TipKorpice == TipKorpice.Voce) vocnaObjavljena = true;
            }
            catch (Exception ex) when (ex is DomainException or DbUpdateException)
            {
                log.LogWarning(ex, "Automatska objava: {Korpica} za {Sedmica}/{Godina} ostaje nacrt.", k.Name, sedmica, godina);
                // Neuspjeli SaveChanges ostavlja izmjene u kontekstu; sljedeća korpica bi ih ponovo poslala.
                db.ChangeTracker.Clear();
            }
        }

        var prvaVocna = vocnaObjavljena && !await VoceProsleSedmiceAsync(godina, sedmica, ct);
        return new AutoObjavaRezultat(godina, sedmica, objavljeno, prvaVocna);
    }

    /// <summary>Generator za jednu korpicu; null kad nije sastavio ništa što se može objaviti.</summary>
    private async Task<Guid?> SastaviAsync(
        GenerisiSedmicniSadrzajHandler generator, Guid korpicaId, string naziv, int godina, int sedmica, bool ponovo, CancellationToken ct)
    {
        var rez = (await generator.Handle(new GenerisiSedmicniSadrzajCommand(godina, sedmica, korpicaId, ponovo), ct))
            .FirstOrDefault();
        if (rez is null) return null;
        if (rez.Upozorenje is not null)
            log.LogInformation("Automatska objava: {Korpica} za {Sedmica}/{Godina}: {Upozorenje}", naziv, sedmica, godina, rez.Upozorenje);
        if (rez.BrojArtikala == 0)
        {
            log.LogWarning("Automatska objava: {Korpica} za {Sedmica}/{Godina} — bazen je prazan, ostaje nacrt.", naziv, sedmica, godina);
            return null;
        }
        return rez.SadrzajId;
    }

    private async Task<bool> VoceProsleSedmiceAsync(int godina, int sedmica, CancellationToken ct)
    {
        var (pg, ps) = SedmicaHelper.Pomjeri(godina, sedmica, -1);
        return await db.SedmicniSadrzaji.AnyAsync(
            s => s.Godina == pg && s.Sedmica == ps
                 && (s.Status == StatusSadrzaja.Objavljen || s.Status == StatusSadrzaja.Zakljucan)
                 && db.Products.Any(p => p.Id == s.KorpicaId && p.TipKorpice == TipKorpice.Voce),
            ct);
    }
}
