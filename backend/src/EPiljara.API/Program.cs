using System.Data.Common;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EPiljara.API.Auth;
using EPiljara.API.Middleware;
using EPiljara.Application;
using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.Common.Models;
using EPiljara.Application.Features.Admin;
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Pretplate;
using EPiljara.Infrastructure;
using EPiljara.Infrastructure.BackgroundJobs;
using EPiljara.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ----- JWT signing key: a strong secret is REQUIRED in production (set via the
// Jwt__SigningKey environment variable). Never ship a usable key in the repo. -----
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
{
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException(
            "Jwt:SigningKey nije postavljen ili je prekratak (min 32 znaka). " +
            "Postavite jak tajni ključ preko environment varijable 'Jwt__SigningKey' u produkciji.");
    jwtSigningKey = "development-only-insecure-jwt-signing-key-change-me";
}
builder.Configuration["Jwt:SigningKey"] = jwtSigningKey; // used by both JWT validation and JwtTokenService

// ----- Application + Infrastructure -----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddHostedService<ExpiredGuestRequestsCleaner>();
builder.Services.AddHostedService<ZakljucavanjeSadrzajaService>();

// ----- Controllers + JSON envelope -----
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

// FluentValidation (via the MediatR pipeline) owns request validation, so responses
// carry our envelope, field keys ("customer.name") and Bosnian messages. Suppressing
// the built-in filter stops ASP.NET from short-circuiting with its own 400 first.
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

// ----- Korpice (generator sedmičnog sadržaja) -----
builder.Services.Configure<KorpiceOpcije>(
    builder.Configuration.GetSection(KorpiceOpcije.Sekcija));

// ----- Pretplate (prag popusta, koliko unaprijed se pravi narudžba) -----
builder.Services.Configure<PretplateOpcije>(
    builder.Configuration.GetSection(PretplateOpcije.Sekcija));

// ----- JWT authentication -----
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // reject alg-confusion (none/RS256)
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "sub",
        };
    });
builder.Services.AddAuthorization();

// ----- CORS (Next.js web shop) -----
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddPolicy("frontend", p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

// ----- Rate limiting -----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
    // AddFixedWindowLimiter registers ONE shared limiter for every request carrying this
    // policy, unlike the global limiter above which is partitioned per IP. AuthController
    // applies "auth" at class level (login, register, /auth/me on every page load) and this
    // branch added guest-request/confirm/track to the same policy, so an unpartitioned
    // limiter would mean 10 requests/min for the entire site combined — a handful of
    // concurrent visitors would 429 each other on login and on "Naruči". Partition by IP,
    // mirroring the global limiter's shape (including its "anonymous" fallback for a null
    // RemoteIpAddress) so the limit is per visitor, as PRODUCTION.md claims.
    options.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // /geocode je anoniman (checkout rade i gosti), pa ga ovo drži da ne postane otvoren
    // relej ka Nominatimu. Granica je iznad stvarne upotrebe — picker je debounce-ovan na
    // 800 ms, pa jedan čovjek koji popunjava adresu napravi svega nekoliko poziva — ali
    // daleko ispod onoga što bi skripta tražila.
    options.AddFixedWindowLimiter("geocode", o =>
    {
        o.PermitLimit = 30;
        o.Window = TimeSpan.FromMinutes(1);
    });
});

// ----- Swagger (with JWT) -----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "E piljara API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

var app = builder.Build();

// ----- Migrate + seed on startup -----
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        // Demo data (incl. accounts with known passwords) seeds in Development, or when
        // explicitly opted in with Seed:Demo=true (e.g. a staging/test deploy). It stays OFF
        // by default in production, so real deployments never get a default admin account.
        var seedDemo = app.Environment.IsDevelopment()
            || string.Equals(builder.Configuration["Seed:Demo"], "true", StringComparison.OrdinalIgnoreCase);
        if (seedDemo)
            await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database initialization failed");
    }
}

// ----- Magacin: bez ispravne tačke aplikacija se ne diže -----
// Ovo je nasljednik ValidateOnStart provjere koja je stajala uz AddOptions dok su
// koordinate bile u appsettings.json. Namjerno stoji IZVAN try/catch bloka iznad:
// on greške samo loguje, pa bi provjera unutra bila bez zuba. Ruta računata iz
// 0°N 0°E, ili iz tuđeg grada, izgleda savršeno uvjerljivo — zato je pad na startu
// bolji od tihe pogrešne ture.
//
// Nedostupna baza se i dalje samo loguje (kao i prije), jer je blok iznad očigledno
// tako zamišljen: API koji se digne bez baze može barem reći da je živ. Razlika je
// u tome što baza koja odgovori, a nema ispravan magacin, sada zaustavlja start.
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var magacin = await MagacinCitac.UcitajAsync(db, CancellationToken.None);
        if (!GeoBounds.Contains(magacin.Latitude, magacin.Longitude))
            throw new InvalidOperationException(
                $"Magacin je na {magacin.Latitude}, {magacin.Longitude} — izvan Bosne i "
                + "Hercegovine. Ispravi ga u admin panelu ili u tabeli \"Magacin\" prije pokretanja.");
    }
    catch (DbException ex)
    {
        app.Logger.LogError(ex, "Provjera magacina nije mogla doći do baze");
    }
}

// ----- Pipeline -----
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts(); // Strict-Transport-Security in non-dev (TLS terminated at the proxy/host)
}
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
