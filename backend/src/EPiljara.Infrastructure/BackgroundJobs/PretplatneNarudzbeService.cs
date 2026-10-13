using EPiljara.Application.Features.Pretplate;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Svakih 15 minuta pravi narudžbe za pretplate kojima je došla sedmica. Isti obrazac kao
/// <see cref="ZakljucavanjeSadrzajaService"/> — oba guarda ispod su odatle, ne izmišljena ovdje.
///
/// Interval je kratak namjerno: komanda je idempotentna, pa čest prolaz ništa ne košta, a
/// znači da se posao oporavlja sam ako je API bio ugašen baš u trenutku roka.
/// </summary>
public class PretplatneNarudzbeService(
    IServiceScopeFactory scopeFactory,
    ILogger<PretplatneNarudzbeService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                var broj = await mediator.Send(new NapraviPretplatneNarudzbeCommand(), stoppingToken);
                if (broj > 0) log.LogInformation("Pretplatnih narudžbi napravljeno: {Broj}", broj);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pad jednog prolaza ne smije ubiti servis — sljedeći prolaz pokušava ponovo.
                log.LogError(ex, "Pravljenje pretplatnih narudžbi nije uspjelo.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
