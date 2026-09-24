using System.Net;
using System.Net.Mail;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Sends transactional email as HTML over SMTP, configured entirely from the <c>Email</c>
/// settings section. Dev targets a local Mailpit catcher (localhost:1025, view at :8025);
/// prod points at a real provider (Gmail/SendGrid/… on :587 with UseSsl + credentials).
/// Transport errors are logged and swallowed so email never blocks the caller's flow —
/// except for <see cref="SendGuestOrderRequestAsync"/>, where a failed send must reach
/// the caller (see the comment above that method).
/// </summary>
public class SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger) : IEmailService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            logger.LogInformation("Order {Code}: nema email adrese — preskačem slanje.", order.Code);
            return Task.CompletedTask;
        }
        return SendHtmlAsync(
            order.CustomerEmail,
            $"Potvrda narudžbe {order.Code} — E piljara",
            OrderEmailTemplate.Build(order),
            $"potvrda narudžbe {order.Code}",
            cancellationToken);
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return Task.CompletedTask;

        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/confirm-email?token={Uri.EscapeDataString(token)}";
        return SendHtmlAsync(
            user.Email,
            "Potvrdite email adresu — E piljara",
            ConfirmEmailTemplate.Build(user, link),
            "potvrda email adrese",
            cancellationToken);
    }

    // Za razliku od potvrde narudžbe, ovaj mejl ne smije tiho pasti: bez njega
    // kupac nema nijedan način da potvrdi, a narudžba nikad ne nastane. Zato
    // izuzetak ide naviše i handler odbija zahtjev.
    public Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken cancellationToken = default)
    {
        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/potvrda/{request.Token}";
        return SendHtmlOrThrowAsync(
            request.CustomerEmail,
            "Potvrdi svoju narudžbu — E piljara",
            GuestOrderEmailTemplate.Build(request, link),
            "potvrda narudžbe gosta",
            cancellationToken);
    }

    private async Task SendHtmlAsync(string to, string subject, string html, string what, CancellationToken ct)
    {
        try
        {
            await SendHtmlOrThrowAsync(to, subject, html, what, ct);
        }
        catch (Exception ex)
        {
            var host = config["Email:SmtpHost"] ?? "localhost";
            var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 1025;
            logger.LogError(ex, "Slanje emaila '{What}' na {To} nije uspjelo ({Host}:{Port}).", what, to, host, port);
        }
    }

    /// <summary>
    /// Stvarno slanje, bez hvatanja izuzetaka transporta. <see cref="SendHtmlAsync"/> je
    /// omotava u try/catch za tokove kojima mejl ne smije blokirati pozivaoca;
    /// <see cref="SendGuestOrderRequestAsync"/> je zove direktno jer njoj neuspjeh mora
    /// stići do pozivaoca.
    /// </summary>
    private async Task SendHtmlOrThrowAsync(string to, string subject, string html, string what, CancellationToken ct)
    {
        var host = config["Email:SmtpHost"] ?? "localhost";
        var port = int.TryParse(config["Email:SmtpPort"], out var p) ? p : 1025;
        var useSsl = bool.TryParse(config["Email:UseSsl"], out var s) && s;
        var username = config["Email:Username"];
        var password = config["Email:Password"];
        var from = config["Email:From"] ?? "narudzbe@epiljara.ba";
        var fromName = config["Email:FromName"] ?? "E piljara";

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = html,
            IsBodyHtml = true,
        };
        message.To.Add(to);

        // 10s, not the 100s default: this runs behind a public anonymous endpoint
        // (guest checkout) and behind flows whose failures are swallowed, so a hanging
        // mail server must fail fast rather than tie up a request (and, for the guest
        // request, a pooled DB connection) for a minute and a half.
        using var client = new SmtpClient(host, port) { EnableSsl = useSsl, Timeout = 10_000 };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message, ct);
        logger.LogInformation("Email '{What}' poslan na {To} preko {Host}:{Port} (ssl={Ssl}).",
            what, to, host, port, useSsl);
    }
}
