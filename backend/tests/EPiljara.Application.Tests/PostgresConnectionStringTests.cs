using EPiljara.Infrastructure.Persistence;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace EPiljara.Application.Tests;

public class PostgresConnectionStringTests
{
    [Fact]
    public void Npgsql_oblik_prolazi_nepromijenjen()
    {
        const string s = "Host=ep-x.neon.tech;Database=epiljara;Username=u;Password=p;SSL Mode=Require;Trust Server Certificate=true";
        PostgresConnectionString.Normalizuj(s).Should().Be(s);
    }

    [Fact]
    public void Neon_URI_se_prevodi_u_Npgsql_oblik()
    {
        var b = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalizuj(
            "postgresql://neondb_owner:ab%40c@ep-cool-1-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        b.Host.Should().Be("ep-cool-1-pooler.eu-central-1.aws.neon.tech");
        b.Port.Should().Be(5432);
        b.Database.Should().Be("neondb");
        b.Username.Should().Be("neondb_owner");
        b.Password.Should().Be("ab@c");
        b.SslMode.Should().Be(SslMode.Require);
    }
}
