using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Infrastructure.Persistence;
using EPiljara.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddOptions<EPiljara.Application.Features.Admin.Magacin>()
            .Bind(config.GetSection("Magacin"))
            // Bez koordinata magacina cijela ruta bi se racunala od 0°N 0°E, usred
            // Atlantika, a rezultat bi izgledao uvjerljivo. Bolje da se aplikacija
            // ne digne nego da tiho vozi iz pogresne tacke.
            .Validate(m => GeoBounds.Contains(m.Latitude, m.Longitude),
                "Magacin:Latitude i Magacin:Longitude moraju biti unutar Bosne i Hercegovine.")
            .ValidateOnStart();

        // Rezerva se registruje zasebno jer je OsrmRouteOptimizer prima kroz konstruktor.
        services.AddSingleton<ZoneRouteOptimizer>();
        services.AddHttpClient<IRouteOptimizer, OsrmRouteOptimizer>(c =>
        {
            c.BaseAddress = new Uri("https://router.project-osrm.org/");
            // Javni demo server; predstavljamo se kao i kod geokodiranja.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(c =>
        {
            // Nominatim odbija zahtjeve bez User-Agenta koji identifikuje aplikaciju.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
