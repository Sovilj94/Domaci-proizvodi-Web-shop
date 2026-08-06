using EPiljara.Domain.Entities;

namespace EPiljara.Application.Common.Interfaces;

/// <summary>
/// Sends an order confirmation to the customer over Viber — used as a fallback when the
/// customer left no email (the phone number is always present). The dev implementation
/// logs the message; swap for a Viber Business Messages provider (Infobip/Twilio/…) in prod.
/// </summary>
public interface IViberService
{
    Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default);
}
