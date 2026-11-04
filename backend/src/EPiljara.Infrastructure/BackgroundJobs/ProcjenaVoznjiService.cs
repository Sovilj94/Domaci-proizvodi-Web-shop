using EPiljara.Application.Features.Voznje;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Svakih 15 minuta preračuna procjene dnevnih tura čiji se skup stanica promijenio, da analitika
/// ima kilometražu i za dane koje niko nije otvorio. Isti obrazac kao <see cref="FiskalizacijaService"/>.
/// </summary>
public class ProcjenaVoznjiService(IServiceScopeFactory scopeFactory, ILogger<ProcjenaVoznjiService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new OsvjeziProcjeneVoznjiCommand(), stoppingToken);
                // Plan za sutra vozačima — od podešenog sata, jednom po vozaču i danu.
                await sender.Send(new EPiljara.Application.Features.Mejlovi.PosaljiPlanoveVozacaCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Osvježavanje procjena vožnji nije uspjelo.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
