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
/// te sedmice (vidi <see cref="ZakljucajNarudzbeCommand"/>) — od tada idu u nabavku — pa sam
/// sastavlja i objavljuje sadržaj sedmice koja se tada naručuje (<see cref="AutoObjaviSadrzajeCommand"/>).
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
                // Odmah poslije zaključavanja se otvara sljedeća sedmica: sadržaj se sam sastavi i objavi.
                try
                {
                    var auto = await mediator.Send(new AutoObjaviSadrzajeCommand(), stoppingToken);
                    if (auto.Objavljeno > 0)
                        log.LogInformation("Automatski objavljeno sadržaja korpica za {Sedmica}/{Godina}: {Broj}",
                            auto.Sedmica, auto.Godina, auto.Objavljeno);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Automatska objava sadržaja korpica nije uspjela.");
                }
                var sedmice = await mediator.Send(new ZakljucajNarudzbeCommand(), stoppingToken);
                // Poslije kraja dostave: narudžbe kojima niko nije promijenio status → neuspjela dostava.
                var neisporucene = await mediator.Send(new OznaciNeisporuceneCommand(), stoppingToken);
                if (neisporucene > 0) log.LogInformation("Označeno neuspjelih dostava poslije kraja dostave: {Broj}", neisporucene);
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
