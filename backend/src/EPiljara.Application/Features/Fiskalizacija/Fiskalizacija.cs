using System.Text.Encodings.Web;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Features.Fiskalizacija;

/// <summary>Stavke fiskalnog računa iz narudžbe. Spec: docs/superpowers/specs/2026-11-04-fiskalizacija-design.md.</summary>
public static class FiskalniSastav
{
    internal static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Artikli (korpica kao jedna stavka) + „Dostava" ako je naplaćena. Promo popust i poeni se raspoređuju
    /// srazmjerno iznosu stavke; zaokruživanje ide na najveću stavku, pa je zbir tačno <c>TotalAmount</c>.
    /// </summary>
    public static IReadOnlyList<FiskalnaStavka> Stavke(Order o, string poreskaOznaka)
    {
        var redovi = o.Items
            .Select(i => (Naziv: i.ProductName, Kolicina: i.Quantity, Jedinica: JedinicaTekst(i.Unit), Cijena: i.UnitPrice))
            .ToList();
        if (o.DeliveryFee > 0) redovi.Add(("Dostava", 1m, "kom", o.DeliveryFee));

        var iznosi = redovi.Select(r => Math.Round(r.Kolicina * r.Cijena, 2)).ToList();
        var bruto = iznosi.Sum();
        var popust = Math.Min(o.Popust + o.PoeniPopust, bruto);
        var popusti = iznosi.Select(x => bruto == 0 ? 0m : Math.Round(popust * x / bruto, 2)).ToList();
        if (popusti.Count > 0)
        {
            var najveca = iznosi.IndexOf(iznosi.Max());
            popusti[najveca] += popust - popusti.Sum();
        }

        return redovi.Select((r, i) => new FiskalnaStavka(
            r.Naziv, r.Kolicina, r.Jedinica, r.Cijena, popusti[i], iznosi[i] - popusti[i], poreskaOznaka)).ToList();
    }

    private static string JedinicaTekst(Unit u) => u switch { Unit.Kg => "kg", Unit.L => "l", _ => "kom" };

    public static IReadOnlyList<FiskalnaStavka> Procitaj(string json) =>
        JsonSerializer.Deserialize<List<FiskalnaStavka>>(json, Json) ?? [];

    public static string Zapisi(IReadOnlyList<FiskalnaStavka> stavke) => JsonSerializer.Serialize(stavke, Json);
}

public static class FiskalizacijaPravila
{
    /// <summary>
    /// Zove se PRIJE <c>SaveChangesAsync</c> onoga ko mijenja status (kao BrojacIsporuka): prelaz u Isporučeno
    /// upisuje prometni račun NaCekanju u isti upis. Ponovno postavljanje istog statusa ne pravi drugi račun.
    /// Narudžba mora imati učitane stavke.
    /// </summary>
    public static async Task ZabiljeziPromjenuStatusaAsync(IAppDbContext db, Order order, OrderStatus novi, CancellationToken ct)
    {
        if (novi != OrderStatus.Delivered || order.Status == OrderStatus.Delivered) return;
        if (order.TotalAmount <= 0) return;
        if (await db.FiskalniRacuni.AnyAsync(r => r.OrderId == order.Id && r.Vrsta == VrstaFiskalnogRacuna.Promet, ct)) return;

        // Bez reda podešavanja isporuka se ipak zatvara (kao kod brojača) — račun ide bez oznake, a dobavljač
        // će ga odbiti i admin to vidi kao grešku, umjesto da vozač ne može zatvoriti stanicu.
        var p = await db.Podesavanja.AsNoTracking().FirstOrDefaultAsync(ct);
        var oznaka = p is null ? string.Empty : p.FiskalPdvObveznik ? p.FiskalOznakaPdv : p.FiskalOznakaBezPdv;
        db.FiskalniRacuni.Add(new FiskalniRacun
        {
            OrderId = order.Id,
            Vrsta = VrstaFiskalnogRacuna.Promet,
            Iznos = order.TotalAmount,
            StavkeJson = FiskalniSastav.Zapisi(FiskalniSastav.Stavke(order, oznaka)),
        });
    }

    /// <summary>Razmak do sljedećeg pokušaja po broju dosadašnjih: 1, 2, 5, 10, 30, pa 60 minuta.</summary>
    public static TimeSpan Razmak(int pokusaja) => TimeSpan.FromMinutes(pokusaja switch
    {
        <= 1 => 1, 2 => 2, 3 => 5, 4 => 10, 5 => 30, _ => 60,
    });
}

/* ---------- obrada (posao) ---------- */

/// <summary>Šalje dobavljaču račune na čekanju i one čiji je ponovni pokušaj došao. Vraća broj izdatih.</summary>
public record ObradiFiskalneRacuneCommand(DateTime? Sada = null) : IRequest<int>;

public class ObradiFiskalneRacuneHandler(IAppDbContext db, IFiskalniDobavljac dobavljac, IEmailService email)
    : IRequestHandler<ObradiFiskalneRacuneCommand, int>
{
    private const int Paket = 20;

    public async Task<int> Handle(ObradiFiskalneRacuneCommand c, CancellationToken ct)
    {
        var sada = c.Sada ?? DateTime.UtcNow;
        var racuni = await db.FiskalniRacuni
            .Where(r => r.Status != StatusFiskalnogRacuna.Izdat && !r.TrajnaGreska
                        && (r.SljedeciPokusaj == null || r.SljedeciPokusaj <= sada))
            .OrderBy(r => r.Kreiran)
            .Take(Paket)
            .ToListAsync(ct);

        var izdato = 0;
        foreach (var r in racuni)
        {
            FiskalniRacun? promet = null;
            if (r.Vrsta == VrstaFiskalnogRacuna.Refundacija)
            {
                promet = await db.FiskalniRacuni.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.RefundiraniRacunId, ct);
                if (promet?.Status != StatusFiskalnogRacuna.Izdat) continue; // čeka prometni
            }

            FiskalniOdgovor odgovor;
            try
            {
                odgovor = await dobavljac.IzdajAsync(new FiskalniZahtjev(
                    r.Id, r.Vrsta, r.NacinPlacanja, FiskalniSastav.Procitaj(r.StavkeJson), r.Iznos,
                    promet?.Broj, promet?.Izdat), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                odgovor = new FiskalniOdgovor(false, Greska: $"Dobavljač nije odgovorio: {ex.Message}");
            }

            r.Pokusaja++;
            r.Dobavljac = dobavljac.Naziv;
            if (odgovor.Uspjeh)
            {
                r.Status = StatusFiskalnogRacuna.Izdat;
                r.Broj = odgovor.Broj;
                r.Brojac = odgovor.Brojac;
                r.Izdat = odgovor.Vrijeme ?? sada;
                r.QrSadrzaj = odgovor.QrSadrzaj;
                r.VerifikacijaUrl = odgovor.VerifikacijaUrl;
                r.ZadnjaGreska = null;
                r.SljedeciPokusaj = null;
                izdato++;
            }
            else
            {
                r.Status = StatusFiskalnogRacuna.Greska;
                r.ZadnjaGreska = (odgovor.Greska ?? "Nepoznata greška dobavljača.") is var g && g.Length > 1000 ? g[..1000] : g;
                r.TrajnaGreska = odgovor.Trajna;
                r.SljedeciPokusaj = sada + FiskalizacijaPravila.Razmak(r.Pokusaja);
            }
            // Svaki račun zasebno: ako padne upis drugog, prvi izdat ne smije ostati „na čekanju" (bio bi izdat dvaput).
            await db.SaveChangesAsync(ct);

            // Trajna greška čeka čovjeka — javi radnicima jednom po pokušaju.
            if (!odgovor.Uspjeh && odgovor.Trajna)
            {
                var kod = await db.Orders.Where(o => o.Id == r.OrderId).Select(o => o.Code).FirstOrDefaultAsync(ct) ?? "";
                await Mejlovi.ObavjestenjaPravila.FiskalnaGreskaAsync(db, email, r, kod, ct);
            }
        }
        return izdato;
    }
}

/* ---------- admin akcije ---------- */

/// <summary>Račun sa greškom (i trajnom) šalje se odmah pri sljedećem prolazu posla.</summary>
public record PonoviFiskalniRacunCommand(Guid Id) : IRequest<Guid>;

public class PonoviFiskalniRacunHandler(IAppDbContext db) : IRequestHandler<PonoviFiskalniRacunCommand, Guid>
{
    public async Task<Guid> Handle(PonoviFiskalniRacunCommand c, CancellationToken ct)
    {
        var r = await db.FiskalniRacuni.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
                ?? throw new NotFoundException("Fiskalni račun nije pronađen.");
        if (r.Status == StatusFiskalnogRacuna.Izdat)
            throw new ConflictException("Račun je već izdat.", "racun_izdat");
        r.TrajnaGreska = false;
        r.SljedeciPokusaj = null;
        await db.SaveChangesAsync(ct);
        return r.Id;
    }
}

/// <summary>Refundacija cijelog prometnog računa narudžbe (povrat novca). Jedna po narudžbi.</summary>
public record RefundirajFiskalniRacunCommand(Guid OrderId) : IRequest<Guid>;

public class RefundirajFiskalniRacunHandler(IAppDbContext db) : IRequestHandler<RefundirajFiskalniRacunCommand, Guid>
{
    public async Task<Guid> Handle(RefundirajFiskalniRacunCommand c, CancellationToken ct)
    {
        var promet = await db.FiskalniRacuni.AsNoTracking()
            .FirstOrDefaultAsync(r => r.OrderId == c.OrderId && r.Vrsta == VrstaFiskalnogRacuna.Promet, ct)
            ?? throw new NotFoundException("Narudžba nema fiskalni račun.");
        if (promet.Status != StatusFiskalnogRacuna.Izdat)
            throw new ConflictException("Prometni račun još nije izdat — refundacija može tek poslije.", "promet_nije_izdat");
        if (await db.FiskalniRacuni.AnyAsync(r => r.OrderId == c.OrderId && r.Vrsta == VrstaFiskalnogRacuna.Refundacija, ct))
            throw new ConflictException("Ova narudžba je već refundirana.", "vec_refundirano");

        var r = new FiskalniRacun
        {
            OrderId = c.OrderId,
            Vrsta = VrstaFiskalnogRacuna.Refundacija,
            Iznos = promet.Iznos,
            NacinPlacanja = promet.NacinPlacanja,
            StavkeJson = promet.StavkeJson,
            RefundiraniRacunId = promet.Id,
        };
        db.FiskalniRacuni.Add(r);
        await db.SaveChangesAsync(ct);
        return r.Id;
    }
}
