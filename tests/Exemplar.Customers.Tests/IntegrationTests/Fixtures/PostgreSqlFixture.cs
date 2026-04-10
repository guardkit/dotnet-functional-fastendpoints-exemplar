using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Exemplar.Customers.Tests.IntegrationTests.Fixtures;

/// <summary>
/// xUnit collection fixture that starts a single PostgreSQL container
/// shared across all integration tests in the collection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "PostgreSql";
}

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
            """);
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("TRUNCATE TABLE customers RESTART IDENTITY;");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
