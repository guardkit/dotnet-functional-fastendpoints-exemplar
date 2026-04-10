using Dapper;
using Exemplar.Addresses.Application;
using Exemplar.E2E.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Exemplar.E2E.Tests;

/// <summary>
/// End-to-end tests for the two Address endpoints.
/// Each test acquires a real JWT from the Keycloak TestContainer and hits the full API.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class AddressEndpointsE2ETests : IAsyncLifetime
{
    private readonly E2EFixture _fixture;

    public AddressEndpointsE2ETests(E2EFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        // Clear any Authorization header left by a previous test — shared HttpClient.
        _fixture.Client.DefaultRequestHeaders.Authorization = null;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // ── POST /api/v1/customers/{id}/addresses ─────────────────────────────────

    [Fact]
    public async Task AddAddress_AdminToken_Returns201()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var customerId = await SeedCustomerAsync();

        var response = await _fixture.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var dto = await response.Content.ReadFromJsonAsync<AddressDto>();
        dto!.CustomerId.Should().Be(customerId);
        dto.Line1.Should().Be("10 High Street");
    }

    [Fact]
    public async Task AddAddress_UserToken_Returns201()
    {
        // AddAddress allows both 'user' and 'admin' roles.
        var adminToken = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);
        var customerId = await SeedCustomerAsync();

        var userToken = await _fixture.GetUserTokenAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"api/v1/customers/{customerId}/addresses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        request.Content = JsonContent.Create(MakeAddressBody());

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddAddress_NoToken_Returns401()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"api/v1/customers/{Guid.NewGuid()}/addresses");
        request.Content = JsonContent.Create(MakeAddressBody());

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v1/customers/{id}/addresses ──────────────────────────────────

    [Fact]
    public async Task GetAddresses_AdminToken_ReturnsAddresses()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var customerId = await SeedCustomerAsync();
        await _fixture.Client.PostAsJsonAsync(
            $"api/v1/customers/{customerId}/addresses", MakeAddressBody());

        var response = await _fixture.Client.GetAsync(
            $"api/v1/customers/{customerId}/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<AddressDto>>();
        dtos.Should().HaveCount(1);
        dtos![0].CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task GetAddresses_NoToken_Returns401()
    {
        var response = await _fixture.Client.GetAsync(
            $"api/v1/customers/{Guid.NewGuid()}/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a customer directly via the Customer API endpoint.
    /// The fixture client must already have an admin token set.
    /// </summary>
    private async Task<Guid> SeedCustomerAsync()
    {
        var response = await _fixture.Client.PostAsJsonAsync(
            "api/v1/customers",
            new { Name = "Seed Customer", Email = $"seed-{Guid.NewGuid():N}@example.com" });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private static object MakeAddressBody(bool isPrimary = false) => new
    {
        Line1       = "10 High Street",
        Line2       = (string?)null,
        City        = "London",
        PostalCode  = "SW1A 1AA",
        Country     = "GB",
        IsPrimary   = isPrimary
    };
}
