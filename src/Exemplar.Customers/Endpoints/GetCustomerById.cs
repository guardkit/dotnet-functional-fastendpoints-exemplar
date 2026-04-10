using Exemplar.Core.Endpoints;
using Exemplar.Customers.Application;
using FastEndpoints;

namespace Exemplar.Customers.Endpoints;

public sealed class GetCustomerByIdRequest
{
    public Guid Id { get; set; }
}

/// <summary>GET /api/v1/customers/{id} — requires authenticated user.</summary>
public sealed class GetCustomerById : Endpoint<GetCustomerByIdRequest, CustomerDto>
{
    private readonly ICustomerService _service;

    public GetCustomerById(ICustomerService service) => _service = service;

    public override void Configure()
    {
        Get("api/v1/customers/{id}"); // {{TEMPLATE: RoutePrefixes}} / {{TEMPLATE: ApiVersion}}
        // No AllowAnonymous() — the global default policy (RequireAuthenticatedUser) enforces auth.
    }

    public override async Task HandleAsync(GetCustomerByIdRequest req, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(req.Id, ct);
        if (result.IsSuccess)
            await Send.OkAsync(result.Value, ct);
        else
            await this.HandleErrorAsync(result.Error, ct);
    }
}
