using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Pretplate;

/// <summary>
/// Pravi narudžbe za sve pretplate kojima je došla sedmica. Zove je pozadinski posao
/// svakih 15 minuta, pa je prolaz koji ništa ne nađe normalno stanje, a ne greška.
///
/// Ovo je jedino mjesto u sistemu koje pravi narudžbu BEZ kupca pred ekranom. Greška se
/// zato ne vidi kao poruka na ekranu nego kao kutija na stolu i račun pouzećem — otuda
/// idempotentnost u transakciji i `continue` umjesto `throw` na svakom koraku.
/// </summary>
public record NapraviPretplatneNarudzbeCommand : IRequest<int>;

public class NapraviPretplatneNarudzbeHandler(
    IAppDbContext db,
    IMediator mediator,
    IOptions<KorpiceOpcije> korpiceOpcije,
    IOptions<PretplateOpcije> pretplateOpcije,
    ILogger<NapraviPretplatneNarudzbeHandler> log)
    : IRequestHandler<NapraviPretplatneNarudzbeCommand, int>
{
    public async Task<int> Handle(NapraviPretplatneNarudzbeCommand _, CancellationToken ct)
    {
        var o = pretplateOpcije.Value;

        // Rok je svojstvo ciklusa, ne pojedine pretplate — isti izvor istine koji koristi
        // checkout (SedmicaHelper), pa pretplatna i ručna narudžba nikad ne mogu završiti u
        // različitim sedmicama za isti trenutak. Čita se jednom po prolazu.
        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);
        var (godina, sedmica) = SedmicaHelper.SedmicaZaNarudzbu(dan, vrijemeOd, cutoff);
        var rok = SedmicaHelper.DatumIsporuke(godina, sedmica, dan, vrijemeOd).AddHours(-cutoff);

        // Prerano: narudžba se pravi tek u prozoru od `SatiUnaprijed` prije roka, da kupac
        // ima vremena da pauzira ili otkaže prije nego što narudžba uopšte nastane.
        if (DateTime.UtcNow < rok.AddHours(-o.SatiUnaprijed)) return 0;

        var kandidati = await db.Pretplate
            .Where(p => p.Status != StatusPretplate.Otkazana)
            .ToListAsync(ct);

        var napravljeno = 0;
        foreach (var p in kandidati)
        {
            try
            {
                if (await ObradiAsync(p, godina, sedmica, ct)) napravljeno++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Jedna pokvarena pretplata (ugašena zona, obrisana korpica, zona čiji je
                // minimum iznad cijene korpice) ne smije ostaviti ostale kupce bez isporuke.
                log.LogError(ex, "Pretplata {Id} nije obrađena.", p.Id);

                // Transakcija je vratila bazu, ali izmjene koje smo stigli da upišemo u
                // OBJEKAT (`Poslednja*`, status poslije pauze) ostaju u kontekstu i otišle bi
                // u bazu pri prvom narednom SaveChanges — to jest, pri sljedećoj pretplati u
                // ovoj istoj petlji. Tada bi pretplata imala upisanu sedmicu za koju narudžba
                // ne postoji i ta sedmica bi joj bila trajno preskočena.
                try { await db.Pretplate.Entry(p).ReloadAsync(ct); }
                catch (Exception ex2) when (ex2 is not OperationCanceledException)
                {
                    log.LogError(ex2, "Pretplata {Id}: ni osvježavanje nije uspjelo.", p.Id);
                }
            }
        }

        return napravljeno;
    }

    private async Task<bool> ObradiAsync(Pretplata p, int godina, int sedmica, CancellationToken ct)
    {
        // Pauza se gleda prije statusa: pauzirana pretplata kojoj je pauza istekla sama
        // prelazi u aktivnu. Da je redoslijed obrnut, pauza bi bila tihi otkaz.
        if (p.Status == StatusPretplate.Pauzirana)
        {
            var pauzaTraje = p.PauzaDoGodina is int pg && p.PauzaDoSedmica is int ps
                && SedmicaHelper.SedmicaPrije(godina, sedmica, pg, ps);
            if (pauzaTraje) return false;

            // Predji briše i PauzaDo — vidi Pretplata.Predji.
            p.Predji(StatusPretplate.Aktivna);
            await db.SaveChangesAsync(ct);
        }

        if (p.Status != StatusPretplate.Aktivna) return false;

        // Jeftina provjera prvo; skupa (upit nad narudžbama) odmah za njom. Dvije provjere
        // namjerno: `Poslednja*` je brz filter, a tabela narudžbi je istina koja preživi i
        // pad između dva upisa. Jedna bez druge je ili spora ili lakovjerna.
        if (p.PoslednjaGodina == godina && p.PoslednjaSedmica == sedmica) return false;

        var vecPostoji = await db.Orders
            .AnyAsync(x => x.PretplataId == p.Id && x.Godina == godina && x.Sedmica == sedmica, ct);
        if (vecPostoji)
        {
            // Narudžba postoji a brojač ne zna za nju: upis je negdje pukao. Popravi tiho.
            p.PoslednjaGodina = godina;
            p.PoslednjaSedmica = sedmica;
            await db.SaveChangesAsync(ct);
            return false;
        }

        var korpica = await db.Products.FirstOrDefaultAsync(x => x.Id == p.ProductId, ct);
        if (korpica is null || !korpica.IsAvailable)
        {
            log.LogWarning("Pretplata {Id}: korpica nije dostupna, sedmica se preskače.", p.Id);
            return false;
        }

        // Bez objavljenog sadržaja narudžba bi bila kutija koju niko ne zna napuniti —
        // radije nema isporuke nego prazna kutija (odluka O6 u specu). Zaključan sadržaj
        // prolazi: to je objavljen sadržaj kojem je rok istekao, a pretplata ide za sedmicu
        // čiji rok tek dolazi.
        var sadrzajSpreman = await db.SedmicniSadrzaji.AnyAsync(
            s => s.KorpicaId == p.ProductId && s.Godina == godina && s.Sedmica == sedmica
                 && (s.Status == StatusSadrzaja.Objavljen || s.Status == StatusSadrzaja.Zakljucan),
            ct);
        if (!sadrzajSpreman)
        {
            log.LogWarning(
                "Pretplata {Id}: sadržaj za {Godina}/{Sedmica} nije objavljen, sedmica se preskače.",
                p.Id, godina, sedmica);
            return false;
        }

        var kupac = await db.Users.FirstOrDefaultAsync(u => u.Id == p.UserId, ct)
            ?? throw new InvalidOperationException($"Pretplata {p.Id} nema korisnika.");

        // Narudžba i upis `Poslednja*` moraju proći ili pasti zajedno. Razdvojeno bi pad
        // između njih dao kupcu dvije korpice i dva računa pouzećem pri narednom prolazu.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var cmd = new CreateOrderCommand(
            new CustomerInput(kupac.Name, kupac.Email, kupac.Phone ?? string.Empty),
            p.DeliveryZoneId,
            p.DeliverySlotId,
            p.DeliveryAddress,
            p.Napomena,
            [new OrderItemInput(p.ProductId, 1m)],
            p.Latitude,
            p.Longitude,
            CustomerId: p.UserId,
            PretplataId: p.Id);

        await mediator.Send(cmd, ct);

        p.PoslednjaGodina = godina;
        p.PoslednjaSedmica = sedmica;
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return true;
    }
}
