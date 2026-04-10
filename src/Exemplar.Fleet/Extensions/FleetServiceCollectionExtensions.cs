using Exemplar.Fleet.Registry;
using Exemplar.Fleet.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Client.Serializers.Json;

namespace Exemplar.Fleet.Extensions;

public static class FleetServiceCollectionExtensions
{
    /// <summary>
    /// Registers all fleet integration services.
    /// Configuration is read from the "Fleet" section of <paramref name="configuration"/>.
    ///
    /// Services registered:
    ///   - <see cref="NatsConnection"/> (singleton) — shared NATS connection
    ///   - <see cref="INatsConnection"/> (singleton alias)
    ///   - <see cref="IManifestRegistry"/> → <see cref="InMemoryManifestRegistry"/> (singleton)
    ///   - <see cref="INatsEventPublisher"/> → <see cref="NatsEventPublisher"/> (singleton)
    ///   - <see cref="INatsAgentClient"/> → <see cref="NatsAgentClient"/> (singleton)
    ///   - <see cref="FleetDiscoveryService"/> (hosted service)
    /// </summary>
    public static IServiceCollection AddFleetIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FleetOptions>(configuration.GetSection("Fleet"));

        // NATS connection — singleton, shared across all fleet services.
        services.AddSingleton<NatsConnection>(sp =>
        {
            var natsUrl = configuration["Fleet:NatsUrl"] ?? "nats://localhost:4222";
            var opts = NatsOpts.Default with
            {
                Url = natsUrl,
                SerializerRegistry = NatsJsonSerializerRegistry.Default,
            };
            return new NatsConnection(opts);
        });

        services.AddSingleton<INatsConnection>(sp => sp.GetRequiredService<NatsConnection>());

        // Registry and services.
        services.AddSingleton<IManifestRegistry, InMemoryManifestRegistry>();
        services.AddSingleton<INatsEventPublisher, NatsEventPublisher>();
        services.AddSingleton<INatsAgentClient, NatsAgentClient>();

        // Background discovery service.
        services.AddHostedService<FleetDiscoveryService>();

        return services;
    }
}
