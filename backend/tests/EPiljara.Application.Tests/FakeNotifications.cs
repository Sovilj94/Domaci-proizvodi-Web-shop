using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;

namespace EPiljara.Application.Tests;

/// <summary>Bilježi šta bi bilo poslano, ne šalje ništa.</summary>
internal sealed class FakeEmail : IEmailService
{
    public List<Order> Narudzbe { get; } = [];
    public List<GuestOrderRequest> ZahtjeviGosta { get; } = [];
    public List<(User Kupac, string Korpica, DateTime Isporuka)> PreskoceneSedmice { get; } = [];

    /// <summary>Simulira pad SMTP slanja, npr. za testiranje kompenzacionog brisanja zahtjeva gosta.</summary>
    public bool BaciGresku { get; set; }

    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        Narudzbe.Add(order);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendPreskocenaIsporukaAsync(User kupac, string korpica, DateTime isporuka, bool korpicaUPonudi = true, CancellationToken ct = default)
    {
        PreskoceneSedmice.Add((kupac, korpica, isporuka));
        return Task.CompletedTask;
    }

    public Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken ct = default)
    {
        if (BaciGresku)
            throw new InvalidOperationException("Simulirani pad slanja mejla.");

        ZahtjeviGosta.Add(request);
        return Task.CompletedTask;
    }
}

internal sealed class FakeViber : IViberService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default) =>
        Task.CompletedTask;
}
