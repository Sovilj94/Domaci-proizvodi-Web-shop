using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` scaffold migrations without a running app.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EPILJARA_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=epiljara;Username=epiljara;Password=epiljara_dev_lozinka";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
