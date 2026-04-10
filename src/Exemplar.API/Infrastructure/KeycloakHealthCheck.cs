using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Exemplar.API.Infrastructure;

/// <summary>
/// Verifies that the Keycloak instance is reachable by fetching its OIDC
/// discovery document (/.well-known/openid-configuration).
/// Tagged "ready" so it participates in /health/ready but not /health/live.
/// </summary>
public sealed class KeycloakHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _discoveryUrl;

    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;

        var authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException(
                "Authentication:Authority is required for KeycloakHealthCheck.");

        _discoveryUrl = authority.TrimEnd('/') + "/.well-known/openid-configuration";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(_discoveryUrl, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Keycloak OIDC discovery endpoint is reachable.")
                : HealthCheckResult.Degraded(
                    $"Keycloak returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Keycloak OIDC discovery endpoint is unreachable.", ex);
        }
    }
}
