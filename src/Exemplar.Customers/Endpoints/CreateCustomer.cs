using Exemplar.Core.Endpoints;
using Exemplar.Customers.Application;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Customers.Endpoints;

/// <summary>POST /api/v1/customers — Admin only. Returns 201 with Location header on success.</summary>
public sealed class CreateCustomer : Endpoint<CreateCustomerRequest, CustomerDto>
{
    private readonly ICustomerService _service;

    public CreateCustomer(ICustomerService service) => _service = service;

    public override void Configure()
    {
        Post("api/v1/customers");
        Roles("Admin");
    }

    public override async Task HandleAsync(CreateCustomerRequest req, CancellationToken ct)
    {
        var result = await _service.CreateCustomerAsync(req, ct);
        if (result.IsSuccess)
        {
            HttpContext.Response.Headers.Location = $"/api/v1/customers/{result.Value.Id}";
            await Send.ResponseAsync(result.Value, StatusCodes.Status201Created, ct);
        }
        else
        {
            await this.HandleErrorAsync(result.Error, ct);
        }
    }
}
