using Microsoft.Extensions.Hosting;
using Serilog;

namespace Exemplar.Core.Infrastructure;

public static class SerilogExtensions
{
    public static IHostBuilder UseSerilogStructuredLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.OpenTelemetry();
            // Serilog.Sinks.OpenTelemetry reads OTEL_EXPORTER_OTLP_ENDPOINT automatically.
        });
    }
}
