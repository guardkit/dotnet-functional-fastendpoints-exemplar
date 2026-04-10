using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Exemplar.Core.Infrastructure;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddOpenTelemetryServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        // OTEL_EXPORTER_OTLP_ENDPOINT drives both Aspire Dashboard (local dev) and
        // generic OTLP (CI/production). No separate exporter configuration required.
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                // Npgsql instrumentation added in Wave 5 (Database setup)
                // tracing.AddNpgsql();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                        options.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
