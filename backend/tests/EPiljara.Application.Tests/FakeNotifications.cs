using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Tests;

/// <summary>Bilježi šta bi bilo poslano, ne šalje ništa.</summary>
internal sealed class FakeEmail : IEmailService
{
    public List<Order> Narudzbe { get; } = [];
    public List<GuestOrderRequest> ZahtjeviGosta { get; } = [];
    public List<(User Kupac, string Korpica, DateTime Isporuka)> PreskoceneSedmice { get; } = [];
    public List<(IReadOnlyList<User> Admini, IReadOnlyList<(string Korpica, int Pretplata)> Stavke, DateTime Rok)> AlarmiSadrzaja { get; } = [];

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

    public Task SendAlarmSadrzajaAsync(IReadOnlyList<User> admini, IReadOnlyList<(string Korpica, int Pretplata)> stavke, DateTime rok, CancellationToken ct = default)
    {
        AlarmiSadrzaja.Add((admini, stavke, rok));
        return Task.CompletedTask;
    }

    public List<(Order Order, OrderStatus Status, bool Automatski)> Statusi { get; } = [];
    public List<(IReadOnlyList<string> Primaoci, string Vrsta, string Naslov, IReadOnlyList<string> Redovi, string? Kljuc)> Obavjestenja { get; } = [];
    public List<(User User, string Token)> ResetLozinke { get; } = [];
    public List<(EmailKampanja Kampanja, IReadOnlyList<PrimalacKampanje> Primaoci)> Kampanje { get; } = [];
    public List<string> Probni { get; } = [];

    public Task SendStatusNarudzbeAsync(Order order, OrderStatus status, bool automatski = false, CancellationToken ct = default)
    {
        Statusi.Add((order, status, automatski));
        return Task.CompletedTask;
    }

    public Task SendObavjestenjeAsync(IReadOnlyList<string> primaoci, string vrsta, string naslov, IReadOnlyList<string> redovi,
        string? dugme = null, string? putanja = null, string? kljuc = null, CancellationToken ct = default)
    {
        Obavjestenja.Add((primaoci, vrsta, naslov, redovi, kljuc));
        return Task.CompletedTask;
    }

    public Task SendResetLozinkeAsync(User user, string token, CancellationToken ct = default)
    {
        ResetLozinke.Add((user, token));
        return Task.CompletedTask;
    }

    public Task SendKampanjaAsync(EmailKampanja kampanja, IReadOnlyList<PrimalacKampanje> primaoci, CancellationToken ct = default)
    {
        Kampanje.Add((kampanja, primaoci));
        return Task.CompletedTask;
    }

    public Task SendProbniAsync(string primalac, CancellationToken ct = default)
    {
        Probni.Add(primalac);
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
