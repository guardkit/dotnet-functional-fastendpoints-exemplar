using Exemplar.Customers.Application;
using Exemplar.Customers.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exemplar.Customers.Infrastructure;

public static class CustomersServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Customers BC services.
    /// CustomerService is registered as both ICustomerService and ICustomerLookup
    /// so the same scoped instance is resolved for both interfaces per request.
    /// </summary>
    public static IServiceCollection AddCustomers(
        this IServiceCollection services,
        string connectionString)
    {
        // Resolve IConfiguration at scoped-service creation time so that
        // WebApplicationFactory test overrides applied after Build() take effect.
        services.AddScoped<ICustomerRepository>(sp =>
        {
            var cs = sp.GetService<IConfiguration>()?.GetConnectionString("DefaultConnection")
                     ?? connectionString;
            return new CustomerRepository(cs);
        });
        services.AddScoped<CustomerService>();
        services.AddScoped<ICustomerService>(sp => sp.GetRequiredService<CustomerService>());
        services.AddScoped<ICustomerLookup>(sp => sp.GetRequiredService<CustomerService>());
        return services;
    }
}
