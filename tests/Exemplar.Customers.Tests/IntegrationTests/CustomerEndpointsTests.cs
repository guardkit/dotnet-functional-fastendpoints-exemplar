using Exemplar.Customers.Application;
using Exemplar.Customers.Tests.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Exemplar.Customers.Tests.IntegrationTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class CustomerEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _db;
    private readonly CustomerApiFixture _api = new();

    public CustomerEndpointsTests(PostgreSqlFixture db) => _db = db;

    public async Task InitializeAsync()
    {
        await _db.ResetAsync();
        await _api.InitializeAsync(_db.ConnectionString);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    // ── POST /api/v1/customers ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateCustomer_WithValidData_Returns201WithLocationHeader()
    {
        var request = new { Name = "Alice Smith", Email = "alice@example.com" };

        var response = await _api.Client.PostAsJsonAsync("api/v1/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Alice Smith");
        dto.Email.Should().Be("alice@example.com");
        dto.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateEmail_Returns409()
    {
        var request = new { Name = "Bob", Email = "duplicate@example.com" };
        await _api.Client.PostAsJsonAsync("api/v1/customers", request);

        var response = await _api.Client.PostAsJsonAsync("api/v1/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidEmail_Returns400()
    {
        var request = new { Name = "Charlie", Email = "not-an-email" };

        var response = await _api.Client.PostAsJsonAsync("api/v1/customers", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/v1/customers/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetCustomerById_WhenExists_Returns200WithDto()
    {
        var created = await CreateCustomerAsync("Diana", "diana@example.com");

        var response = await _api.Client.GetAsync($"api/v1/customers/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetCustomerById_WhenNotFound_Returns404()
    {
        var response = await _api.Client.GetAsync($"api/v1/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/v1/customers ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCustomers_ReturnsAllCustomers()
    {
        await CreateCustomerAsync("Eve", "eve@example.com");
        await CreateCustomerAsync("Frank", "frank@example.com");

        var response = await _api.Client.GetAsync("api/v1/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<CustomerDto>>();
        dtos.Should().HaveCountGreaterOrEqualTo(2);
    }

    // ── PUT /api/v1/customers/{id}/deactivate ──────────────────────────────────

    [Fact]
    public async Task DeactivateCustomer_WhenActive_Returns200WithInactiveStatus()
    {
        var created = await CreateCustomerAsync("Grace", "grace@example.com");

        var response = await _api.Client.PutAsync(
            $"api/v1/customers/{created.Id}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task DeactivateCustomer_WhenAlreadyInactive_Returns409()
    {
        var created = await CreateCustomerAsync("Henry", "henry@example.com");
        await _api.Client.PutAsync($"api/v1/customers/{created.Id}/deactivate", null);

        var response = await _api.Client.PutAsync(
            $"api/v1/customers/{created.Id}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeactivateCustomer_WhenNotFound_Returns404()
    {
        var response = await _api.Client.PutAsync(
            $"api/v1/customers/{Guid.NewGuid()}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private async Task<CustomerDto> CreateCustomerAsync(string name, string email)
    {
        var response = await _api.Client.PostAsJsonAsync(
            "api/v1/customers", new { Name = name, Email = email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }
}
