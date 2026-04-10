using Exemplar.Addresses.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Exemplar.Addresses.Infrastructure;

public static class AddressesServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Addresses BC services.
    /// Caller must have already registered <c>ICustomerLookup</c> (e.g. via <c>AddCustomers</c>)
    /// before calling this method, as <c>AddressService</c> depends on it.
    /// </summary>
    public static IServiceCollection AddAddresses(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<IAddressRepository>(_ => new AddressRepository(connectionString));
        services.AddScoped<IAddressService, AddressService>();
        return services;
    }
}
