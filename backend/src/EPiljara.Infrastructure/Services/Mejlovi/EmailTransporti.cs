using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EPiljara.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EPiljara.Infrastructure.Services.Mejlovi;

/// <summary>Pošiljalac iz konfiguracije: <c>Email:From</c>, <c>Email:FromName</c>, opciono <c>Email:ReplyTo</c>.</summary>
public sealed record EmailPosiljalac(string Adresa, string Ime, string? OdgovorNa)
{
    public static EmailPosiljalac Iz(IConfiguration config) => new(
        config["Email:From"] is { Length: > 0 } f ? f : "narudzbe@epiljara.ba",
        config["Email:FromName"] is { Length: > 0 } n ? n : "E piljara",
        config["Email:ReplyTo"] is { Length: > 0 } r ? r : null);
}

/// <summary>
/// SMTP: u razvoju Mailpit (localhost:1025, pregled na :8025), a može i bilo koji SMTP server sa nalogom.
/// Na Render free NE radi — tamo je odlazni SMTP blokiran; koristi se <see cref="BrevoEmailTransport"/>.
/// </summary>
public sealed class SmtpEmailTransport(IConfiguration config) : IEmailTransport
{
    public string Naziv => "smtp";

    public async Task<EmailRezultat> PosaljiAsync(EmailZaSlanje p, CancellationToken ct)
    {
        var host = config["Email:SmtpHost"] ?? "localhost";
        var port = int.TryParse(config["Email:SmtpPort"], out var pt) ? pt : 1025;
        var od = EmailPosiljalac.Iz(config);
        using var poruka = new MailMessage
        {
            From = new MailAddress(od.Adresa, od.Ime),
            Subject = p.Naslov,
            Body = p.Html,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8,
        };
        try
        {
            poruka.To.Add(p.PrimalacIme is null ? new MailAddress(p.Primalac) : new MailAddress(p.Primalac, p.PrimalacIme));
        }
        catch (FormatException)
        {
            return new EmailRezultat(false, Greska: $"Neispravna adresa primaoca: {p.Primalac}", Trajna: true);
        }
        if (od.OdgovorNa is not null) poruka.ReplyToList.Add(od.OdgovorNa);

        // 15 s: spor server ne smije zaglaviti red, a sljedeći krug ionako pokušava ponovo.
        using var klijent = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(config["Email:UseSsl"], out var s) && s,
            Timeout = 15_000,
        };
        if (config["Email:Username"] is { Length: > 0 } korisnik)
            klijent.Credentials = new NetworkCredential(korisnik, config["Email:Password"]);
        try
        {
            await klijent.SendMailAsync(poruka, ct);
            return new EmailRezultat(true);
        }
        catch (SmtpFailedRecipientException ex)
        {
            return new EmailRezultat(false, Greska: ex.Message, Trajna: true);
        }
        catch (SmtpException ex)
        {
            return new EmailRezultat(false, Greska: $"SMTP {host}:{port}: {ex.Message}");
        }
    }
}

/// <summary>
/// Brevo (ex Sendinblue) transakcioni API preko HTTPS-a — radi i tamo gdje je SMTP blokiran (Render free).
/// Besplatno 300 mejlova dnevno. Ključ: <c>Email:Brevo:ApiKey</c>; adresa pošiljaoca mora biti potvrđena
/// u Brevu (ili domen sa SPF/DKIM zapisima), inače Brevo odbija slanje.
/// </summary>
public sealed class BrevoEmailTransport(HttpClient http, IConfiguration config) : IEmailTransport
{
    public string Naziv => "brevo";

    public async Task<EmailRezultat> PosaljiAsync(EmailZaSlanje p, CancellationToken ct)
    {
        var od = EmailPosiljalac.Iz(config);
        using var zahtjev = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { name = od.Ime, email = od.Adresa },
                to = new[] { new { email = p.Primalac, name = p.PrimalacIme ?? p.Primalac } },
                replyTo = od.OdgovorNa is null ? null : new { email = od.OdgovorNa },
                subject = p.Naslov,
                htmlContent = p.Html,
                headers = new Dictionary<string, string> { ["X-EPiljara-Id"] = p.Id.ToString("N") },
            }, options: new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
        };
        zahtjev.Headers.Add("api-key", config["Email:Brevo:ApiKey"]);
        zahtjev.Headers.Accept.ParseAdd("application/json");

        HttpResponseMessage odgovor;
        try
        {
            odgovor = await http.SendAsync(zahtjev, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new EmailRezultat(false, Greska: $"Brevo nije odgovorio: {ex.Message}");
        }

        var tijelo = await odgovor.Content.ReadAsStringAsync(ct);
        if (odgovor.IsSuccessStatusCode)
        {
            string? id = null;
            try { id = JsonDocument.Parse(tijelo).RootElement.GetProperty("messageId").GetString(); } catch { }
            return new EmailRezultat(true, id);
        }

        var kod = (int)odgovor.StatusCode;
        var poruka = $"Brevo {kod}: {Skrati(tijelo)}";
        // 400 = neispravan zahtjev/adresa (ponavljanje ne pomaže). 401/403 = ključ ili nepotvrđen pošiljalac:
        // NIJE trajno — kad admin popravi podešavanje, mejlovi iz reda odu sami. 402/429/5xx = kasnije.
        return new EmailRezultat(false, Greska: poruka, Trajna: kod == 400);
    }

    private static string Skrati(string s) => s.Length <= 400 ? s : s[..400];
}

/// <summary>Budi posao slanja čim je mejl upisan. Najviše jedno „buđenje" na čekanju je dovoljno.</summary>
public sealed class EmailSignal : IEmailSignal
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Probudi()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { }
    }

    public async Task CekajAsync(TimeSpan najduze, CancellationToken ct) => await _signal.WaitAsync(najduze, ct);
}

/// <summary>HMAC-SHA256 adrese ključem za JWT — link za odjavu ne može da se napravi za tuđu adresu.</summary>
public sealed class OdjavaPotpis(IConfiguration config) : IOdjavaPotpis
{
    private byte[] Kljuc => Encoding.UTF8.GetBytes("odjava|" + (config["Jwt:SigningKey"] is { Length: > 0 } k ? k : "epiljara-razvoj"));

    public string Potpisi(string email)
    {
        var hes = HMACSHA256.HashData(Kljuc, Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToBase64String(hes, 0, 18).Replace('+', '-').Replace('/', '_');
    }

    public bool Provjeri(string email, string potpis) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Potpisi(email)), Encoding.UTF8.GetBytes(potpis.Trim()));
}
