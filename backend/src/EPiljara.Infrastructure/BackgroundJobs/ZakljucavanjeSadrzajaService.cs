using EPiljara.Application.Features.Nabavka;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Svakih 15 minuta zatvara sadržaje kojima je rok naručivanja prošao i zaključava narudžbe
/// te sedmice (vidi <see cref="ZakljucajNarudzbeCommand"/>) — od tada idu u nabavku.
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
                var sedmice = await mediator.Send(new ZakljucajNarudzbeCommand(), stoppingToken);
                // Otkupni listovi nastaju čim se sedmica zaključa — po jedan za svako gazdinstvo.
                // Sedmica bez listova se vraća u svakom prolazu dok ih ne dobije, pa pad jedne
                // sedmice ne prekida ostale i sljedeći prolaz pokušava ponovo.
                foreach (var s in sedmice)
                {
                    try
                    {
                        var listova = await mediator.Send(new NapraviOtkupneListoveCommand(s.Godina, s.Sedmica), stoppingToken);
                        if (listova > 0) log.LogInformation("Otkupnih listova za sedmicu {Sedmica}/{Godina}: {Broj}", s.Sedmica, s.Godina, listova);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        log.LogError(ex, "Otkupni listovi za sedmicu {Sedmica}/{Godina} nisu napravljeni.", s.Sedmica, s.Godina);
                    }
                }
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
