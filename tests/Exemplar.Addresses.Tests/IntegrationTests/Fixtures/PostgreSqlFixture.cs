using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Exemplar.Addresses.Tests.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "AddressesPostgreSql";
}

/// <summary>
/// xUnit collection fixture: single PostgreSQL container shared across all integration tests.
/// Creates both <c>customers</c> and <c>addresses</c> tables — addresses tests need the
/// customers table because <c>AddressService</c> calls <c>ICustomerLookup</c>.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ApplySchemaAsync();
    }

    private async Task ApplySchemaAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS customers (
                id         UUID        PRIMARY KEY,
                name       TEXT        NOT NULL,
                email      TEXT        NOT NULL UNIQUE,
                status     INTEGER     NOT NULL DEFAULT 0,
                created_at TIMESTAMP   NOT NULL
            );

            CREATE TABLE IF NOT EXISTS addresses (
                id          UUID        PRIMARY KEY,
                customer_id UUID        NOT NULL,
                line1       TEXT        NOT NULL,
                line2       TEXT,
                city        TEXT        NOT NULL,
                postal_code TEXT        NOT NULL,
                country     TEXT        NOT NULL,
                is_primary  BOOLEAN     NOT NULL DEFAULT false,
                created_at  TIMESTAMP   NOT NULL
            );
            """);
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        // Truncate addresses first (no FK constraint, but ordering is clear intent)
        await conn.ExecuteAsync("""
            TRUNCATE TABLE addresses;
            TRUNCATE TABLE customers;
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
