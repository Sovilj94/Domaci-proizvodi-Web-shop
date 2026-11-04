using EPiljara.Application.Features.Fiskalizacija;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Svakih 30 sekundi predaje dobavljaču fiskalne račune na čekanju i one čiji je ponovni pokušaj
/// došao. Isti obrazac kao <see cref="ZakljucavanjeSadrzajaService"/>.
/// </summary>
public class FiskalizacijaService(IServiceScopeFactory scopeFactory, ILogger<FiskalizacijaService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var izdato = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new ObradiFiskalneRacuneCommand(), stoppingToken);
                if (izdato > 0) log.LogInformation("Izdato fiskalnih računa: {Broj}", izdato);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Obrada fiskalnih računa nije uspjela.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
