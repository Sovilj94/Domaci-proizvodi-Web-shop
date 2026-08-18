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
        services.AddSingleton<IRouteOptimizer, ZoneRouteOptimizer>();
        services.AddScoped<ISocialAuthVerifier, SocialAuthVerifier>();

        services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(c =>
        {
            // Nominatim odbija zahtjeve bez User-Agenta koji identifikuje aplikaciju.
            c.DefaultRequestHeaders.UserAgent.ParseAdd("EPiljara/1.0 (dostava Banja Luka)");
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
