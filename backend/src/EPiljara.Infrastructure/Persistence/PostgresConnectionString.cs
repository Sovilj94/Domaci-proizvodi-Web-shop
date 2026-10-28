using Npgsql;

namespace EPiljara.Infrastructure.Persistence;

/// <summary>
/// Prima connection string u Npgsql obliku (<c>Host=…;Username=…</c>) ili u URI obliku koji
/// Neon i većina hostova nude za kopiranje (<c>postgresql://user:pass@host/db?sslmode=require</c>).
/// Npgsql sam ne razumije URI, pa ga ovdje prevodimo; Npgsql oblik prolazi nepromijenjen.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalizuj(string connectionString)
    {
        var s = connectionString.Trim();
        if (!s.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return s;

        var uri = new Uri(s);
        var korisnik = uri.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(korisnik[0]),
            Password = korisnik.Length > 1 ? Uri.UnescapeDataString(korisnik[1]) : null,
        };

        foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = par.Split('=', 2);
            var kljuc = Uri.UnescapeDataString(kv[0]).ToLowerInvariant();
            var vrijednost = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (kljuc == "sslmode" && Enum.TryParse<SslMode>(vrijednost.Replace("-", ""), true, out var ssl))
                b.SslMode = ssl;
            // channel_binding, options i ostali libpq parametri nemaju smisla za Npgsql — preskaču se.
        }

        return b.ConnectionString;
    }
}
