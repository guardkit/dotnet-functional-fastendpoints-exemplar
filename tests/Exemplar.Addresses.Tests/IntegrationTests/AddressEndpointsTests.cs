using Dapper;
using Exemplar.Addresses.Application;
using Exemplar.Addresses.Tests.IntegrationTests.Fixtures;
using FluentAssertions;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Exemplar.Addresses.Tests.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class AddressEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _db;
    private readonly AddressApiFixture _api = new();

    public AddressEndpointsTests(PostgreSqlFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        await _db.ResetAsync();
        await _api.InitializeAsync(_db.ConnectionString);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    // ── GET /api/v1/customers/{id}/addresses ───────────────────────────────────

    [Fact]
    public async Task GetAddresses_WhenNoAddresses_ReturnsEmptyList()
    {
        var customerId = Guid.NewGuid();

        var response = await _api.Client.GetAsync($"api/v1/customers/{customerId}/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<AddressDto>>();
        dtos.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAddresses_AfterAddingAddress_ReturnsAddress()
    {
        var customerId = await SeedCustomerAsync();
        await AddAddressViaApiAsync(customerId);

        var response = await _api.Client.GetAsync($"api/v1/customers/{customerId}/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<AddressDto>>();
        dtos.Should().HaveCount(1);
        dtos![0].CustomerId.Should().Be(customerId);
    }

    // ── POST /api/v1/customers/{id}/addresses ─────────────────────────────────

    [Fact]
    public async Task AddAddress_WithValidData_Returns201WithLocationHeader()
    {
        var customerId = await SeedCustomerAsync();

        var response = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var dto = await response.Content.ReadFromJsonAsync<AddressDto>();
        dto.Should().NotBeNull();
        dto!.Line1.Should().Be("10 High Street");
        dto.City.Should().Be("London");
        dto.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task AddAddress_WithUnknownCustomer_Returns404()
    {
        var unknownId = Guid.NewGuid();

        var response = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{unknownId}/addresses", MakeAddressBody());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddAddress_WhenAddingSecondPrimaryAddress_Returns409()
    {
        var customerId = await SeedCustomerAsync();
        await AddAddressViaApiAsync(customerId, isPrimary: true);

        var response = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody(isPrimary: true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddAddress_WithEmptyLine1_Returns400()
    {
        var customerId = await SeedCustomerAsync();
        var request = new { Line1 = "", City = "London", PostalCode = "SW1A 1AA", Country = "GB", IsPrimary = false };

        var response = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddAddress_MultipleNonPrimary_AllSucceed()
    {
        var customerId = await SeedCustomerAsync();

        var r1 = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody());
        var r2 = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody());

        r1.StatusCode.Should().Be(HttpStatusCode.Created);
        r2.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await _api.Client.GetAsync($"api/v1/customers/{customerId}/addresses");
        var dtos = await listResponse.Content.ReadFromJsonAsync<List<AddressDto>>();
        dtos.Should().HaveCount(2);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a customer directly via SQL. AddressApiFixture only registers Addresses endpoints,
    /// so we cannot seed customers through the HTTP layer.
    /// </summary>
    private async Task<Guid> SeedCustomerAsync()
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO customers (id, name, email, status, created_at)
            VALUES (@Id, @Name, @Email, 0, @CreatedAt)
            """,
            new { Id = id, Name = "Seed Customer", Email = $"seed-{id:N}@example.com", CreatedAt = DateTime.UtcNow });
        return id;
    }

    private async Task AddAddressViaApiAsync(Guid customerId, bool isPrimary = false)
    {
        var response = await _api.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody(isPrimary));
        response.EnsureSuccessStatusCode();
    }

    private static object MakeAddressBody(bool isPrimary = false) => new
    {
        Line1 = "10 High Street",
        Line2 = (string?)null,
        City = "London",
        PostalCode = "SW1A 1AA",
        Country = "GB",
        IsPrimary = isPrimary
    };
}
