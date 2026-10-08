using EPiljara.Application.Features.Korpice;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Svakih 15 minuta zatvara sadržaje kojima je rok naručivanja prošao.
/// Zaključavanje mora biti automatsko — ako zavisi od toga da se admin sjeti,
/// prije ili kasnije se neće sjetiti. Isti obrazac kao <see cref="ExpiredGuestRequestsCleaner"/>
/// — oba guarda ispod su odatle preuzeta, ne izmišljena ovdje.
/// </summary>
public class ZakljucavanjeSadrzajaService(
    IServiceScopeFactory scopeFactory,
    ILogger<ZakljucavanjeSadrzajaService> log) : BackgroundService
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
                var broj = await mediator.Send(new ZakljucajSadrzajeCommand(), stoppingToken);
                if (broj > 0) log.LogInformation("Zaključano sadržaja korpica: {Broj}", broj);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pad jednog prolaza ne smije ubiti servis — sljedeći prolaz pokušava ponovo.
                log.LogError(ex, "Zaključavanje sadržaja korpica nije uspjelo.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
