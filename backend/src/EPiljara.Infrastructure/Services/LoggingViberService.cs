using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Dev/stub Viber sender: logs the message instead of sending it (there is no BSP yet).
/// Replace with a real Viber Business Messages provider (Infobip/Twilio/…) implementation
/// of <see cref="IViberService"/> and swap the DI registration — nothing else changes.
/// </summary>
public class LoggingViberService(ILogger<LoggingViberService> logger) : IViberService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            logger.LogWarning("Order {Code}: nema telefona — Viber poruka nije poslana.", order.Code);
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Viber → {Phone} (narudžba {Code}):\n{Message}",
            order.CustomerPhone, order.Code, ViberMessage.Build(order));
        return Task.CompletedTask;
    }
}
