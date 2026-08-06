using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Phase-1 email: logs the confirmation. Swap for an SMTP/SendGrid implementation of
/// <see cref="IEmailService"/> without touching the order flow.
/// </summary>
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Order confirmation → {Email} · {Code} · {Total:0.##} KM · {ItemCount} stavki",
            order.CustomerEmail ?? "(bez emaila)",
            order.Code,
            order.TotalAmount,
            order.Items.Count);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email confirmation → {Email} · token {Token}", user.Email, token);
        return Task.CompletedTask;
    }
}
