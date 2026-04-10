using Exemplar.Core.Endpoints;
using Exemplar.Customers.Application;
using FastEndpoints;

namespace Exemplar.Customers.Endpoints;

public sealed class DeactivateCustomerRequest
{
    public Guid Id { get; set; }
}

/// <summary>PUT /api/v1/customers/{id}/deactivate — Admin only.</summary>
public sealed class DeactivateCustomer : Endpoint<DeactivateCustomerRequest, CustomerDto>
{
    private readonly ICustomerService _service;

    public DeactivateCustomer(ICustomerService service) => _service = service;

    public override void Configure()
    {
        Put("api/v1/customers/{id}/deactivate"); // {{TEMPLATE: RoutePrefixes}}
        Roles("admin"); // {{TEMPLATE: PolicyNames}}
    }

    public override async Task HandleAsync(DeactivateCustomerRequest req, CancellationToken ct)
    {
        var result = await _service.DeactivateCustomerAsync(req.Id, ct);
        if (result.IsSuccess)
            await Send.OkAsync(result.Value, ct);
        else
            await this.HandleErrorAsync(result.Error, ct);
    }
}
