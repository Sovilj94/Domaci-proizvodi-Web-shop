using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;

namespace EPiljara.Application.Tests;

/// <summary>Bilježi šta bi bilo poslano, ne šalje ništa.</summary>
internal sealed class FakeEmail : IEmailService
{
    public List<Order> Narudzbe { get; } = [];
    public List<GuestOrderRequest> ZahtjeviGosta { get; } = [];

    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        Narudzbe.Add(order);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken ct = default)
    {
        ZahtjeviGosta.Add(request);
        return Task.CompletedTask;
    }
}

internal sealed class FakeViber : IViberService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default) =>
        Task.CompletedTask;
}
