using Exemplar.Customers.Application;
using Exemplar.E2E.Tests.Fixtures;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace Exemplar.E2E.Tests;

/// <summary>
/// End-to-end tests for the four Customer endpoints.
/// Each test acquires a real JWT from the Keycloak TestContainer and hits the full API.
/// </summary>
[Collection(E2ECollection.Name)]
public sealed class CustomerEndpointsE2ETests : IAsyncLifetime
{
    private readonly E2EFixture _fixture;

    public CustomerEndpointsE2ETests(E2EFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        // Clear any Authorization header left by a previous test — shared HttpClient.
        _fixture.Client.DefaultRequestHeaders.Authorization = null;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    // ── POST /api/v1/customers ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateCustomer_AdminToken_Returns201()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _fixture.Client.PostAsJsonAsync(
            "api/v1/customers",
            new { Name = "Acme Corp", Email = "contact@acme.com" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto!.Name.Should().Be("Acme Corp");
        dto.Status.Should().Be("Active");
    }

    [Fact]
    public async Task CreateCustomer_UserToken_Returns403()
    {
        // AddAddress uses 'user' role — CreateCustomer requires 'admin' only.
        var token = await _fixture.GetUserTokenAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "api/v1/customers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { Name = "Forbidden Corp", Email = "deny@example.com" });

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCustomer_NoToken_Returns401()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "api/v1/customers");
        request.Content = JsonContent.Create(new { Name = "Anon Corp", Email = "anon@example.com" });

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v1/customers/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetCustomerById_AdminToken_Returns200()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var created = await CreateCustomerAsync("Diana Prince", "diana@example.com");

        var response = await _fixture.Client.GetAsync($"api/v1/customers/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetCustomerById_NoToken_Returns401()
    {
        var response = await _fixture.Client.GetAsync(
            $"api/v1/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v1/customers ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCustomers_AdminToken_Returns200()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        await CreateCustomerAsync("Eve", "eve@example.com");
        await CreateCustomerAsync("Frank", "frank@example.com");

        var response = await _fixture.Client.GetAsync("api/v1/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<CustomerDto>>();
        dtos.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllCustomers_NoToken_Returns401()
    {
        var response = await _fixture.Client.GetAsync("api/v1/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/v1/customers/{id}/deactivate ──────────────────────────────────

    [Fact]
    public async Task DeactivateCustomer_AdminToken_Returns200()
    {
        var token = await _fixture.GetAdminTokenAsync();
        _fixture.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var created = await CreateCustomerAsync("Grace Hopper", "grace@example.com");

        var response = await _fixture.Client.PutAsync(
            $"api/v1/customers/{created.Id}/deactivate", new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<CustomerDto>();
        dto!.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task DeactivateCustomer_UserToken_Returns403()
    {
        var token = await _fixture.GetUserTokenAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"api/v1/customers/{Guid.NewGuid()}/deactivate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _fixture.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateCustomer_NoToken_Returns401()
    {
        var response = await _fixture.Client.PutAsync(
            $"api/v1/customers/{Guid.NewGuid()}/deactivate", new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private async Task<CustomerDto> CreateCustomerAsync(string name, string email)
    {
        var response = await _fixture.Client.PostAsJsonAsync(
            "api/v1/customers",
            new { Name = name, Email = email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }
}
