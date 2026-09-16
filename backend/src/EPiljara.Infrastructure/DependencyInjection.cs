using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Infrastructure.Persistence;
using EPiljara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace EPiljara.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString =
            config.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("EPILJARA_CONNECTION")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nije konfigurisan.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IViberService, LoggingViberService>();
        services.AddScoped<ISocialAuthVerifier, SocialAuthVerifier>();

        // Magacin se više ne konfiguriše — red u bazi je jedini izvor istine, a provjera
        // koja je nekad bila ValidateOnStart sada stoji u Program.cs (MagacinProvjera),
        // odmah poslije migracija, jer tek tada baza može biti pitana.

        // Rezerva se registruje zasebno jer je OsrmRouteOptimizer prima kroz konstruktor.
        services.AddSingleton<ZoneRouteOptimizer>();
        services.AddHttpClient<IRouteOptimizer, OsrmRouteOptimizer>(c =>
        {
            c.BaseAddress = new Uri(
                config["Osrm:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Osrm:BaseUrl nije podešen. Bez njega optimizacija ne bi radila, "
                    + "a ruta bi tiho ispala neoptimizovana."));
            // Sopstvena instanca više ne zahtijeva User-Agent (bio je uslov javnog
            // demo servera), ali ga ostavljamo — bezopasan je i koristan ako se ikad
            // gleda saobraćaj.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(20);
        })
        // Podrazumijevani HttpClientFactory logger ispisuje puni URI zahtjeva na Information
        // nivou, a OSRM koordinate vozača drži u putanji (/trip/v1/{profile}/{coords}), ne u
        // query stringu — nema query filtera koji bi ih sklonio. Uklanja se ovdje, na builderu,
        // namjerno umjesto kroz Logging konfiguraciju: config je najlakše mjesto da neko kasnije
        // "diže" HTTP logging radi dijagnostike ispada, i tačno bi ponovo otvorio ovo curenje.
        // Zamjenski signal za latenciju je eksplicitan LogInformation na uspješnoj grani u
        // OsrmRouteOptimizer, bez koordinata.
        .RemoveAllLoggers();

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(c =>
        {
            c.BaseAddress = new Uri(
                config["Nominatim:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Nominatim:BaseUrl nije podešen. Bez njega geokodiranje ne bi radilo, "
                    + "a adrese bi tiho ostajale bez koordinata."));
            // Nominatim odbija zahtjeve bez User-Agenta koji identifikuje aplikaciju.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(15);
        })
        // Isti razlog kao kod OSRM klijenta: podrazumijevani logeri HttpClientFactory-ja
        // pišu cijeli URI na Information nivou, a u URI-ju je adresa kupca. Ne vraćati.
        .RemoveAllLoggers();

        return services;
    }
}
