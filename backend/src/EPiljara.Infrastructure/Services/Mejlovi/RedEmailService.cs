using System.Globalization;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services.Mejlovi;

/// <summary>
/// <see cref="IEmailService"/> koji NE šalje: napravi gotov mejl (naslov, HTML, pismo kupca) i upiše ga u red
/// (<see cref="EmailPoruka"/>), pa probudi posao slanja. Upis ide kroz ZASEBAN kontekst baze, da ne snimi tuđe
/// nesnimljene izmjene pozivaoca. Greška upisa se loguje i ne ide naviše — osim za zahtjev gosta, kome bez
/// mejla nema potvrde (handler tada poništava zahtjev).
/// </summary>
public class RedEmailService(
    IServiceScopeFactory scopes,
    IConfiguration config,
    IEmailSignal signal,
    IOdjavaPotpis odjava,
    ILogger<RedEmailService> log) : IEmailService
{
    private string BaseUrl => (config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

    private static EmailPoruka Poruka(string primalac, string? ime, string vrsta, (string Naslov, string Html) sadrzaj,
        string? kljuc = null, Guid? orderId = null, Guid? kampanjaId = null) => new()
    {
        Primalac = primalac.Trim(), PrimalacIme = ime, Vrsta = vrsta, Naslov = sadrzaj.Naslov, Html = sadrzaj.Html,
        Kljuc = kljuc, OrderId = orderId, KampanjaId = kampanjaId,
    };

    private async Task UpisiAsync(IReadOnlyList<EmailPoruka> poruke, CancellationToken ct, bool baciGresku = false)
    {
        if (poruke.Count == 0) return;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var kljucevi = poruke.Where(p => p.Kljuc is not null).Select(p => p.Kljuc!).ToList();
            var postoje = kljucevi.Count == 0
                ? []
                : (await db.EmailPoruke.Where(m => m.Kljuc != null && kljucevi.Contains(m.Kljuc)).Select(m => m.Kljuc!).ToListAsync(ct)).ToHashSet();
            var nove = poruke.Where(p => p.Kljuc is null || !postoje.Contains(p.Kljuc)).ToList();
            if (nove.Count == 0) return;
            db.EmailPoruke.AddRange(nove);
            await db.SaveChangesAsync(ct);
            signal.Probudi();
        }
        catch (DbUpdateException ex) when (!baciGresku && poruke.All(p => p.Kljuc is not null))
        {
            // Dva istovremena upisa istog događaja: jedinstveni ključ pusti jedan — ishod je isti.
            log.LogInformation(ex, "Mejl sa ključem već postoji u redu ({Vrsta}).", poruke[0].Vrsta);
        }
        catch (Exception ex) when (!baciGresku && ex is not OperationCanceledException)
        {
            log.LogError(ex, "Mejl '{Vrsta}' za {Primalac} nije upisan u red.", poruke[0].Vrsta, poruke[0].Primalac);
        }
    }

    /* ---------- kupci ---------- */

    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail)) return Task.CompletedTask;
        var trackUrl = string.IsNullOrWhiteSpace(order.PublicToken) ? null : $"{BaseUrl}/narudzba/{order.PublicToken}";
        var sadrzaj = PismoPoruke.Primijeni(order.Pismo, $"Potvrda narudžbe {order.Code} — E piljara", OrderEmailTemplate.Build(order, trackUrl));
        return UpisiAsync([Poruka(order.CustomerEmail, order.CustomerName, "potvrda_narudzbe", sadrzaj, $"potvrda:{order.Id}", order.Id)], ct);
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return Task.CompletedTask;
        var link = $"{BaseUrl}/confirm-email?token={Uri.EscapeDataString(token)}";
        var sadrzaj = PismoPoruke.Primijeni(user.Pismo, "Potvrdi email adresu — E piljara", ConfirmEmailTemplate.Build(user, link));
        return UpisiAsync([Poruka(user.Email, user.Name, "potvrda_adrese", sadrzaj)], ct);
    }

    public Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken ct = default)
    {
        var link = $"{BaseUrl}/potvrda/{request.Token}";
        var sadrzaj = PismoPoruke.Primijeni(request.Pismo, "Potvrdi svoju narudžbu — E piljara", GuestOrderEmailTemplate.Build(request, link));
        // Bez ovog mejla gost nema kako da potvrdi — greška upisa mora do handlera.
        return UpisiAsync([Poruka(request.CustomerEmail, request.CustomerName, "zahtjev_gosta", sadrzaj)], ct, baciGresku: true);
    }

    public Task SendPreskocenaIsporukaAsync(User kupac, string korpica, DateTime isporuka, bool korpicaUPonudi = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kupac.Email)) return Task.CompletedTask;
        var sadrzaj = PismoPoruke.Primijeni(kupac.Pismo, "Ove sedmice nema isporuke — E piljara",
            PreskocenaIsporukaTemplate.Build(kupac, korpica, isporuka, $"{BaseUrl}/moj-nalog/pretplate", korpicaUPonudi));
        return UpisiAsync([Poruka(kupac.Email, kupac.Name, "preskocena_isporuka", sadrzaj)], ct);
    }

    public async Task SendStatusNarudzbeAsync(Order order, OrderStatus status, bool automatski = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail)) return;
        var prati = string.IsNullOrWhiteSpace(order.PublicToken) ? BaseUrl : $"{BaseUrl}/narudzba/{order.PublicToken}";
        var termin = await TerminAsync(order, ct);
        var ime = EmailOkvir.Enc(order.CustomerName.Split(' ')[0]);
        var kod = EmailOkvir.Enc(order.Code);
        (string Naslov, string Telo, string? Dugme, string? Link) m = status switch
        {
            OrderStatus.Confirmed => ($"Narudžba {order.Code} je potvrđena",
                EmailOkvir.Paragraf($"Zdravo {ime}, potvrdili smo vašu narudžbu <b>{kod}</b> ({EmailOkvir.Novac(order.TotalAmount)}).")
                + EmailOkvir.Paragraf(termin is null ? "Javićemo se kad krene isporuka." : $"Stiže <b>{EmailOkvir.Enc(termin)}</b>. Plaćate pouzećem, vozaču na vratima."),
                "Prati narudžbu", prati),
            OrderStatus.Delivered => ($"Hvala! Narudžba {order.Code} je isporučena",
                EmailOkvir.Paragraf($"Zdravo {ime}, narudžba <b>{kod}</b> je uručena. Hvala što kupujete domaće.")
                + EmailOkvir.Paragraf("Kako vam se dopala? Ocjena nam traje 10 sekundi, a gazdinstvima puno znači."),
                "Ocijeni isporuku", prati),
            OrderStatus.FailedDelivery => ($"Nismo vas zatekli — narudžba {order.Code}",
                EmailOkvir.Paragraf($"Zdravo {ime}, vozač je danas pokušao da uruči narudžbu <b>{kod}</b>, ali vas nije zatekao.")
                + EmailOkvir.Paragraf("Odgovorite na ovaj mejl ili nas pozovite pa ćemo dogovoriti novu isporuku."),
                "Pogledaj narudžbu", prati),
            OrderStatus.Cancelled when automatski => ($"Narudžba {order.Code} nije potvrđena do roka",
                EmailOkvir.Paragraf($"Zdravo {ime}, narudžba <b>{kod}</b> nije potvrđena do roka naručivanja, pa je sistem otkazao.")
                + EmailOkvir.Paragraf("Ništa niste platili, a potrošeni poeni su vraćeni na vaš nalog. Rado ćemo vam dostaviti sljedeće sedmice."),
                "Naruči ponovo", BaseUrl),
            OrderStatus.Cancelled => ($"Narudžba {order.Code} je otkazana",
                EmailOkvir.Paragraf($"Zdravo {ime}, narudžba <b>{kod}</b> je otkazana.")
                + EmailOkvir.Paragraf("Ako je ovo greška ili imate pitanje, samo odgovorite na ovaj mejl."),
                "Pogledaj narudžbu", prati),
            _ => (string.Empty, string.Empty, null, null),
        };
        if (m.Naslov.Length == 0) return;
        var sadrzaj = PismoPoruke.Primijeni(order.Pismo, $"{m.Naslov} — E piljara", EmailOkvir.Build("E piljara", m.Naslov, m.Telo, m.Dugme, m.Link));
        await UpisiAsync([Poruka(order.CustomerEmail, order.CustomerName, $"status_{status.ToString().ToLowerInvariant()}", sadrzaj,
            $"status:{order.Id}:{status}", order.Id)], ct);
    }

    /// <summary>„četvrtak 12.11. od 16:00" — iz sedmice narudžbe i termina; null bez termina.</summary>
    private async Task<string?> TerminAsync(Order order, CancellationToken ct)
    {
        var slot = order.DeliverySlot;
        if (slot is null && order.DeliverySlotId != Guid.Empty)
        {
            using var scope = scopes.CreateScope();
            slot = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().DeliverySlots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == order.DeliverySlotId, ct);
        }
        if (slot is null || order.Godina == 0) return null;
        var dan = ISOWeek.ToDateTime(order.Godina, order.Sedmica, (DayOfWeek)(slot.DayOfWeek % 7));
        string[] dani = ["nedjelja", "ponedjeljak", "utorak", "srijeda", "četvrtak", "petak", "subota"];
        return $"{dani[(int)dan.DayOfWeek]} {dan.Day}.{dan.Month}. između {slot.TimeFrom} i {slot.TimeTo}";
    }

    /* ---------- radnici i vozači ---------- */

    public async Task SendAlarmSadrzajaAsync(IReadOnlyList<User> admini, IReadOnlyList<(string Korpica, int Pretplata)> stavke, DateTime rok, CancellationToken ct = default)
    {
        var html = AlarmSadrzajaTemplate.Build(stavke, rok, $"{BaseUrl}/admin/korpice");
        await UpisiAsync(admini.Where(a => !string.IsNullOrWhiteSpace(a.Email))
            .Select(a => Poruka(a.Email, a.Name, "alarm_sadrzaja", ("Sadržaj korpica nije objavljen — E piljara", html))).ToList(), ct);
    }

    public Task SendObavjestenjeAsync(IReadOnlyList<string> primaoci, string vrsta, string naslov, IReadOnlyList<string> redovi,
        string? dugme = null, string? putanja = null, string? kljuc = null, CancellationToken ct = default)
    {
        var telo = string.Concat(redovi.Select(r => EmailOkvir.Paragraf(EmailOkvir.Enc(r))));
        var link = putanja is null ? null : $"{BaseUrl}{putanja}";
        var html = EmailOkvir.Build("E piljara · tim", naslov, telo, dugme, link,
            "Automatsko obavještenje E piljare. Koja obavještenja stižu i kome, mijenja se u adminu → Mejlovi.");
        return UpisiAsync(primaoci.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => Poruka(p, null, vrsta, ($"{naslov} — E piljara", html), kljuc is null ? null : $"{kljuc}:{p.ToLowerInvariant()}"))
            .ToList(), ct);
    }

    public Task SendResetLozinkeAsync(User user, string token, CancellationToken ct = default)
    {
        var link = $"{BaseUrl}/nova-lozinka?token={Uri.EscapeDataString(token)}";
        var telo = EmailOkvir.Paragraf($"Zdravo {EmailOkvir.Enc(user.Name.Split(' ')[0])}, neko je zatražio novu lozinku za vaš nalog na E piljari.")
                   + EmailOkvir.Paragraf("Link važi <b>1 sat</b> i radi samo jednom. Ako niste vi tražili, samo zanemarite ovaj mejl — lozinka ostaje ista.");
        var sadrzaj = PismoPoruke.Primijeni(user.Pismo, "Nova lozinka — E piljara",
            EmailOkvir.Build("E piljara", "Postavi novu lozinku", telo, "Postavi novu lozinku", link));
        return UpisiAsync([Poruka(user.Email, user.Name, "reset_lozinke", sadrzaj)], ct);
    }

    public Task SendKampanjaAsync(EmailKampanja kampanja, IReadOnlyList<PrimalacKampanje> primaoci, CancellationToken ct = default)
    {
        var telo = EmailOkvir.TekstUHtml(kampanja.Tekst);
        var poruke = primaoci.Select(p =>
        {
            var podnozje = p.Odjava
                ? $"Ovaj mejl ste dobili kao kupac ili pretplatnik na novosti E piljare. " +
                  $"<a href=\"{EmailOkvir.Enc($"{BaseUrl}/odjava?e={Uri.EscapeDataString(p.Email)}&p={odjava.Potpisi(p.Email)}")}\" style=\"color:#7a8580\">Odjavi se sa obavještenja</a>."
                : "Ovaj mejl ste dobili jer imate narudžbu, pretplatu ili radite sa E piljarom.";
            var sadrzaj = PismoPoruke.Primijeni(p.Pismo, kampanja.Naslov, EmailOkvir.Build("E piljara", kampanja.Naslov, telo, null, null, podnozje));
            return Poruka(p.Email, p.Ime, "obavjestenje", sadrzaj, $"kampanja:{kampanja.Id:N}:{p.Email}", kampanjaId: kampanja.Id);
        }).ToList();
        return UpisiAsync(poruke, ct);
    }

    public Task SendProbniAsync(string primalac, CancellationToken ct = default)
    {
        var sada = DateTime.UtcNow.ToString("d.M.yyyy. HH:mm 'UTC'", CultureInfo.InvariantCulture);
        var html = EmailOkvir.Build("E piljara · tim", "Probni mejl",
            EmailOkvir.Paragraf($"Ako ovo čitate, slanje mejlova radi ({EmailOkvir.Enc(sada)}).")
            + EmailOkvir.Paragraf("Provjerite i da mejl nije završio u spamu — ako jeste, domen pošiljaoca još nema SPF/DKIM zapise."));
        return UpisiAsync([Poruka(primalac, null, "probni", ("Probni mejl — E piljara", html))], ct);
    }
}
