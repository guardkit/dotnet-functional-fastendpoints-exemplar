using Exemplar.Addresses.Endpoints;
using Exemplar.Addresses.Infrastructure;
using Exemplar.Customers.Infrastructure;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Exemplar.Addresses.Tests.IntegrationTests.Fixtures;

/// <summary>
/// Builds a minimal ASP.NET Core test server with:
/// - Customers services (registers ICustomerLookup — required by AddressService)
/// - Addresses services
/// - FastEndpoints (Addresses assembly only)
/// Authentication is handled by FakeAuthHandler — tests do not need JWT tokens.
/// </summary>
public sealed class AddressApiFixture : IAsyncDisposable
{
    private IHost? _host;
    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync(string connectionString)
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseFastEndpoints(c =>
                    {
                        c.Endpoints.RoutePrefix = string.Empty;
                        c.Errors.UseProblemDetails();
                    });
                });
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(FakeAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>(
                            FakeAuthHandler.SchemeName, _ => { });
                    services.AddAuthorization();
                    // AddCustomers registers ICustomerLookup — AddressService depends on it.
                    services.AddCustomers(connectionString);
                    services.AddAddresses(connectionString);
                    services.AddFastEndpoints(o =>
                        o.Assemblies = [typeof(GetAddressesByCustomerId).Assembly]);
                });
            })
            .StartAsync();

        Client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}

/// <summary>
/// Fake authentication handler that authenticates every request as a User+Admin.
/// </summary>
public sealed class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public FakeAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
