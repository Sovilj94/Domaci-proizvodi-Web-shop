using EPiljara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Briše nepotvrđene narudžbe gosta kojima je isteklo vrijeme. Potvrda se na ovo
/// ne oslanja — ona sama provjerava istek, pa zahtjev koji čeka brisanje ne može
/// proći. Ovaj servis postoji samo da smetlje stvarno nestane iz baze.
/// </summary>
public class ExpiredGuestRequestsCleaner(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredGuestRequestsCleaner> logger) : BackgroundService
{
    private static readonly TimeSpan Razmak = TimeSpan.FromMinutes(5);

    public static async Task<int> ObrisiIstekleAsync(AppDbContext db, DateTime sada, CancellationToken ct)
    {
        var istekli = await db.GuestOrderRequests
            .Where(r => r.ExpiresAt <= sada)
            .ToListAsync(ct);

        if (istekli.Count == 0) return 0;

        db.GuestOrderRequests.RemoveRange(istekli);
        await db.SaveChangesAsync(ct);
        return istekli.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var obrisano = await ObrisiIstekleAsync(db, DateTime.UtcNow, stoppingToken);
                if (obrisano > 0)
                    logger.LogInformation("Obrisano isteklih zahtjeva gosta: {Broj}", obrisano);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pad jednog prolaza ne smije ubiti servis — sljedeći prolaz pokušava ponovo.
                logger.LogError(ex, "Čišćenje isteklih zahtjeva gosta nije uspjelo.");
            }

            try { await Task.Delay(Razmak, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
