using Exemplar.Addresses.Application;
using Microsoft.Extensions.Configuration;
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
        // Resolve IConfiguration at scoped-service creation time so that
        // WebApplicationFactory test overrides applied after Build() take effect.
        services.AddScoped<IAddressRepository>(sp =>
        {
            var cs = sp.GetService<IConfiguration>()?.GetConnectionString("DefaultConnection")
                     ?? connectionString;
            return new AddressRepository(cs);
        });
        services.AddScoped<IAddressService, AddressService>();
        return services;
    }
}
