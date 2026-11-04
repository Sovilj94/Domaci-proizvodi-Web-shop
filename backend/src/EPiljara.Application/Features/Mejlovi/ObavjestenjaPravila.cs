using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Mejlovi;

/// <summary>
/// Ko dobija koje obavještenje i da li je uključeno. Svi pozivi idu POSLIJE snimanja posla koji ih je izazvao —
/// mejl ide u red preko zasebnog konteksta i nikad ne obara posao (greška se samo loguje u servisu za mejl).
/// Bez reda Podesavanja (testna baza) sve je uključeno, a primaoci radnici su admini.
/// </summary>
public static class ObavjestenjaPravila
{
    public static Task<Podesavanja?> PodesavanjaAsync(IAppDbContext db, CancellationToken ct) =>
        db.Podesavanja.AsNoTracking().FirstOrDefaultAsync(ct);

    /// <summary>Adrese iz podešavanja (zarez, tačka-zarez ili novi red); prazno = svi admini sa adresom.</summary>
    public static async Task<List<string>> RadniciAsync(IAppDbContext db, Podesavanja? p, CancellationToken ct)
    {
        var lista = Razdvoji(p?.ObavRadniciPrimaoci);
        if (lista.Count > 0) return lista;
        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.Email != "")
            .Select(u => u.Email)
            .ToListAsync(ct);
    }

    public static List<string> Razdvoji(string? tekst) =>
        (tekst ?? string.Empty)
            .Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();

    /// <summary>Kupcu o promjeni statusa — samo za statuse o kojima se javlja i kad je uključeno.</summary>
    public static async Task StatusNarudzbeAsync(IAppDbContext db, IEmailService email, Order order, OrderStatus stari,
        bool automatski, CancellationToken ct)
    {
        if (order.Status == stari || string.IsNullOrWhiteSpace(order.CustomerEmail)) return;
        var p = await PodesavanjaAsync(db, ct);
        var ukljuceno = order.Status switch
        {
            OrderStatus.Confirmed => p?.ObavKupacPotvrdjena ?? true,
            OrderStatus.Delivered => p?.ObavKupacIsporucena ?? true,
            OrderStatus.FailedDelivery => p?.ObavKupacNeuspjela ?? true,
            OrderStatus.Cancelled => p?.ObavKupacOtkazana ?? true,
            _ => false,
        };
        if (ukljuceno) await email.SendStatusNarudzbeAsync(order, order.Status, automatski, ct);
    }

    public static async Task NovaNarudzbaAsync(IAppDbContext db, IEmailService email, Order order, CancellationToken ct)
    {
        var p = await PodesavanjaAsync(db, ct);
        if (!(p?.ObavRadnikNovaNarudzba ?? true)) return;
        var primaoci = await RadniciAsync(db, p, ct);
        if (primaoci.Count == 0) return;
        var isporuka = order.DeliverySlotId == Guid.Empty ? null : await db.DeliverySlots.AsNoTracking()
            .Where(s => s.Id == order.DeliverySlotId).Select(s => new { s.DayOfWeek, s.TimeFrom, s.TimeTo }).FirstOrDefaultAsync(ct);
        var redovi = new List<string>
        {
            $"{order.CustomerName} · {Novac(order.TotalAmount)}",
            $"{order.Items.Count} {Stavki(order.Items.Count)} · {order.DeliveryAddress}",
            $"Sedmica {order.Sedmica}/{order.Godina}" + (isporuka is null ? "" : $" · {Dan(isporuka.DayOfWeek)} {isporuka.TimeFrom}–{isporuka.TimeTo}"),
        };
        if (!string.IsNullOrWhiteSpace(order.Note)) redovi.Add($"Napomena: {order.Note}");
        await email.SendObavjestenjeAsync(primaoci, "nova_narudzba", $"Nova narudžba {order.Code}", redovi,
            "Otvori narudžbe", "/admin/orders", $"nova:{order.Id}", ct);
    }

    public static async Task OdbijeneAsync(IAppDbContext db, IEmailService email, IReadOnlyList<Order> odbijene, CancellationToken ct)
    {
        if (odbijene.Count == 0) return;
        var p = await PodesavanjaAsync(db, ct);
        if (!(p?.ObavRadnikOdbijene ?? true)) return;
        var primaoci = await RadniciAsync(db, p, ct);
        if (primaoci.Count == 0) return;
        var redovi = odbijene.OrderBy(o => o.Code)
            .Select(o => $"{o.Code} — {o.CustomerName}, {Novac(o.TotalAmount)}").ToList();
        redovi.Insert(0, "Nisu potvrđene do roka naručivanja, pa ih je sistem odbio. Kupci su obaviješteni.");
        await email.SendObavjestenjeAsync(primaoci, "odbijene_narudzbe",
            $"Automatski odbijeno narudžbi: {odbijene.Count}", redovi, "Otvori narudžbe", "/admin/orders", null, ct);
    }

    public static async Task FiskalnaGreskaAsync(IAppDbContext db, IEmailService email, FiskalniRacun r, string kod, CancellationToken ct)
    {
        var p = await PodesavanjaAsync(db, ct);
        if (!(p?.ObavRadnikFiskalnaGreska ?? true)) return;
        var primaoci = await RadniciAsync(db, p, ct);
        if (primaoci.Count == 0) return;
        await email.SendObavjestenjeAsync(primaoci, "fiskalna_greska", $"Fiskalni račun nije izdat — {kod}",
            [
                $"Dobavljač je odbio račun ({(r.Vrsta == VrstaFiskalnogRacuna.Promet ? "promet" : "refundacija")}, {Novac(r.Iznos)}).",
                $"Greška: {r.ZadnjaGreska}",
                "Ispravi uzrok pa klikni „Pokušaj ponovo\" na stranici Fiskalni računi.",
            ],
            "Fiskalni računi", "/admin/fiskalni-racuni", $"fiskal:{r.Id}:{r.Pokusaja}", ct);
    }

    public static async Task OdstupanjeGorivaAsync(IAppDbContext db, IEmailService email, string vozac, DateOnly datum,
        decimal procjena, decimal placeno, decimal odstupanje, string? napomena, CancellationToken ct)
    {
        var p = await PodesavanjaAsync(db, ct);
        if (!(p?.ObavRadnikGorivo ?? true)) return;
        var primaoci = await RadniciAsync(db, p, ct);
        if (primaoci.Count == 0) return;
        var redovi = new List<string>
        {
            $"{vozac}, {datum.ToString("d.M.yyyy.", CultureInfo.InvariantCulture)}",
            $"Procjena {Novac(procjena)}, plaćeno {Novac(placeno)} ({(odstupanje > 0 ? "+" : "")}{odstupanje.ToString("0.#", CultureInfo.GetCultureInfo("sr-Latn"))} %).",
        };
        if (!string.IsNullOrWhiteSpace(napomena)) redovi.Add($"Napomena vozača: {napomena}");
        await email.SendObavjestenjeAsync(primaoci, "odstupanje_goriva", $"Gorivo odstupa od procjene — {vozac}", redovi,
            "Gorivo po danima", $"/admin/poslovanje/troskovi?godina={datum.Year}&mjesec={datum.Month}",
            $"gorivo:{vozac}:{datum:yyyyMMdd}:{placeno}", ct);
    }

    public static string Novac(decimal iznos) => iznos.ToString("0.00", CultureInfo.GetCultureInfo("sr-Latn")) + " KM";

    private static string Stavki(int n) => n % 10 == 1 && n % 100 != 11 ? "stavka" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "stavke" : "stavki";

    public static string Dan(int dayOfWeek) =>
        new[] { "nedjelja", "ponedjeljak", "utorak", "srijeda", "četvrtak", "petak", "subota" }[dayOfWeek % 7];

    /// <summary>Lokalni datum i sat (zona dostave).</summary>
    public static DateTime Lokalno(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), SedmicaHelper.Zona);
}
