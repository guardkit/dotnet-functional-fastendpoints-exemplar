using Dapper;
using Npgsql;
using System.Text.Json;
using Testcontainers.Keycloak;
using Testcontainers.PostgreSql;
using Xunit;

namespace Exemplar.E2E.Tests.Fixtures;

/// <summary>
/// xUnit collection fixture: starts PostgreSQL + Keycloak TestContainers and the
/// <see cref="ExemplarApiFactory"/> once for the entire E2E test suite.
/// </summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Name = "E2E";
}

/// <summary>
/// Manages the lifecycle of all E2E test infrastructure:
/// <list type="bullet">
///   <item>PostgreSQL TestContainer (receives DbUp migrations run by the API on startup)</item>
///   <item>Keycloak TestContainer (issues real JWTs for auth scenarios)</item>
///   <item><see cref="ExemplarApiFactory"/> (WebApplicationFactory over the full API)</item>
/// </list>
/// Call <see cref="ResetAsync"/> in each test class's <c>InitializeAsync</c> to clear data
/// between test classes without restarting the containers.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    // Keycloak realm JSON is copied to the output directory by the .csproj Content item.
    private static readonly string RealmFilePath =
        Path.Combine(AppContext.BaseDirectory, "exemplar-realm.json");

    // Keycloak client secret — must match the value in exemplar-realm.json.
    private const string ClientSecret = "REPLACE_ME_CLIENT_SECRET"; // {{TEMPLATE: KeycloakClientId}}
    private const string Realm        = "exemplar";                 // {{TEMPLATE: KeycloakRealm}}
    private const string ClientId     = "exemplar-api";             // {{TEMPLATE: KeycloakClientId}}

    private PostgreSqlContainer _postgres = null!;
    private KeycloakContainer   _keycloak = null!;
    private ExemplarApiFactory  _factory  = null!;

    /// <summary>Shared HTTP client for all E2E test requests.</summary>
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // 1. Start PostgreSQL and Keycloak in parallel — they have no dependency on each other.
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("exemplar_test")
            .WithUsername("exemplar")
            .WithPassword("test_password")
            .Build();

        _keycloak = new KeycloakBuilder("quay.io/keycloak/keycloak:26.4")
            .WithRealm(RealmFilePath)
            .Build();

        await Task.WhenAll(
            _postgres.StartAsync(),
            _keycloak.StartAsync());

        // 2. Create the factory AFTER containers are ready.
        //    ExemplarApiFactory overrides config to point at the TestContainers instances.
        //    On startup the app runs DbUp migrations against TestContainers PostgreSQL.
        var authority = $"{_keycloak.GetBaseAddress().TrimEnd('/')}/realms/{Realm}";
        _factory = new ExemplarApiFactory(_postgres.GetConnectionString(), authority);
        Client   = _factory.CreateClient();
    }

    /// <summary>Returns a Bearer token for <c>admin_user</c> (realm role: admin).</summary>
    public Task<string> GetAdminTokenAsync() => GetTokenAsync("admin_user", "password");

    /// <summary>Returns a Bearer token for <c>test_user</c> (realm role: user).</summary>
    public Task<string> GetUserTokenAsync() => GetTokenAsync("test_user", "password");

    /// <summary>Truncates all application tables so each test class starts with a clean slate.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        // Truncate both tables in one statement; PostgreSQL resolves FK ordering automatically.
        await conn.ExecuteAsync("TRUNCATE TABLE addresses, customers;");
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _keycloak.DisposeAsync();
    }

    // ── Token acquisition ──────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(string username, string password)
    {
        var tokenEndpoint =
            $"{_keycloak.GetBaseAddress().TrimEnd('/')}/realms/{Realm}/protocol/openid-connect/token";

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type",    "password"),
            new KeyValuePair<string, string>("client_id",     ClientId),
            new KeyValuePair<string, string>("client_secret", ClientSecret),
            new KeyValuePair<string, string>("username",      username),
            new KeyValuePair<string, string>("password",      password),
        ]));

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
