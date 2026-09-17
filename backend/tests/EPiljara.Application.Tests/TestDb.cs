using EPiljara.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// SQLite in-memory baza sa stvarnom šemom. Konekcija se drži otvorenom jer
/// SQLite briše in-memory bazu čim se zadnja konekcija zatvori.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Provjereno (i bez ove linije): Microsoft.Data.Sqlite 9.0.11 sam šalje
        // `PRAGMA foreign_keys=1` čim se ADO.NET konekcija otvori — enforcement
        // stranih ključeva radi i bez ovoga. Linija ostaje kao eksplicitna,
        // verzija-nezavisna garancija (ne oslanjati se na podrazumijevano
        // ponašanje providera za integritet podataka u testovima) i kao
        // dokumentacija namjere za sljedećeg čitaoca. Vidi TestDbTests.cs za
        // test koji stvarno dokazuje da je enforcement uključen.
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
