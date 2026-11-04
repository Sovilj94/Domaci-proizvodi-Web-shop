using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Features.Mejlovi;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Šalje mejlove iz reda: odmah kad ga probudi novi upis, inače svakih 15 sekundi (ponovni pokušaji).
/// Puna serija znači da u redu ima još — sljedeća ide bez čekanja.
/// </summary>
public class SlanjeEmailovaService(IServiceScopeFactory scopeFactory, IEmailSignal signal, ILogger<SlanjeEmailovaService> log)
    : BackgroundService
{
    private const int Serija = 20;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var poslato = 0;
            try
            {
                using var scope = scopeFactory.CreateScope();
                poslato = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new PosaljiEmailoveCommand(Serija: Serija), stoppingToken);
                if (poslato > 0) log.LogInformation("Poslato mejlova: {Broj}", poslato);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Slanje mejlova iz reda nije uspjelo.");
            }

            if (poslato == Serija) continue;
            try { await signal.CekajAsync(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
