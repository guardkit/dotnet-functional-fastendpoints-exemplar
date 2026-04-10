// {{TEMPLATE: ProjectName}} — replace all "Exemplar" namespace prefixes throughout the solution.
using Exemplar.Addresses.Endpoints;
using Exemplar.Addresses.Infrastructure;
using Exemplar.API.Authentication;
using Exemplar.API.Infrastructure;
using Exemplar.Core.Endpoints;
using Exemplar.Core.Infrastructure;
using Exemplar.Customers.Endpoints;
using Exemplar.Customers.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required.");

// 1. Observability first — captures startup events and errors before anything else runs
builder.Services.AddOpenTelemetryServices(builder.Configuration, "Exemplar.API");
builder.Host.UseSerilogStructuredLogging();

// 2. Authentication & authorisation
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorizationPolicies();

// 3. HttpClient factory — required by KeycloakHealthCheck
builder.Services.AddHttpClient();

// 4. Bounded contexts
//    Customers must be registered before Addresses — AddressService depends on ICustomerLookup.
//    CustomerService is registered as both ICustomerService and ICustomerLookup so the same
//    scoped instance is resolved for both interfaces within a single request (no double-instantiation).
builder.Services.AddCustomers(connectionString);
builder.Services.AddAddresses(connectionString);

// 5. FastEndpoints — explicit assembly list keeps discovery deterministic
builder.Services.AddFastEndpointsServices(
    typeof(CreateCustomer).Assembly,   // Exemplar.Customers
    typeof(AddAddress).Assembly);      // Exemplar.Addresses

// 6. Health checks
//    "ready" tag: participates in /health/ready (dependency checks)
//    no tag:      liveness only (/health/live returns 200 as long as process runs)
builder.Services.AddHealthChecks()
    .AddCheck<KeycloakHealthCheck>("keycloak",    tags: new[] { "ready" })
    .AddNpgSql(connectionString,  name: "postgresql", tags: new[] { "ready" });

var app = builder.Build();

// 7. DbUp migrations — run before the app accepts traffic so the schema is always current
app.RunDbMigrations();

// 8. Middleware pipeline
app.UseAuthentication();
app.UseAuthorization();
app.UseApiConfiguration();      // FastEndpoints + Swagger
app.MapHealthEndpoints();       // /health/live  and  /health/ready

app.Run();

// Required so WebApplicationFactory<Program> can reference this type from test assemblies.
// The top-level-statement compiler generates an internal partial Program class; this makes it public.
public partial class Program { }
