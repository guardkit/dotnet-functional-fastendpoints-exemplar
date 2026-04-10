using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Reflection;

namespace Exemplar.Core.Endpoints;

public static class FastEndpointsExtensions
{
    public static IServiceCollection AddFastEndpointsServices(
        this IServiceCollection services,
        params Assembly[] endpointAssemblies)
    {
        services.AddFastEndpoints(o =>
        {
            if (endpointAssemblies.Length > 0)
                o.Assemblies = endpointAssemblies;
        });

        services.SwaggerDocument();

        return services;
    }

    public static WebApplication UseApiConfiguration(this WebApplication app)
    {
        app.UseFastEndpoints(config =>
        {
            // No RoutePrefix — endpoint routes already include the full path (e.g. "api/v1/customers").
            config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            config.Errors.UseProblemDetails();
        });

        app.UseSwaggerGen();

        return app;
    }
}
