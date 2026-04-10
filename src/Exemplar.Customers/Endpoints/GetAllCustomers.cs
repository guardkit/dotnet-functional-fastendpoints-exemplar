using Exemplar.Core.Endpoints;
using Exemplar.Customers.Application;
using FastEndpoints;

namespace Exemplar.Customers.Endpoints;

/// <summary>GET /api/v1/customers — requires authenticated user.</summary>
public sealed class GetAllCustomers : EndpointWithoutRequest<IReadOnlyList<CustomerDto>>
{
    private readonly ICustomerService _service;

    public GetAllCustomers(ICustomerService service) => _service = service;

    public override void Configure()
    {
        Get("api/v1/customers");
        // No AllowAnonymous() — global default policy enforces auth.
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct);
        if (result.IsSuccess)
            await Send.OkAsync(result.Value, ct);
        else
            await this.HandleErrorAsync(result.Error, ct);
    }
}
