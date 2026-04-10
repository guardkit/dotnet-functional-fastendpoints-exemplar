using Exemplar.Addresses.Application;
using Exemplar.Core.Endpoints;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Addresses.Endpoints;

/// <summary>Combined route + body request for POST /api/v1/customers/{id}/addresses.</summary>
public sealed class AddAddressEndpointRequest
{
    public Guid Id { get; set; }  // customer ID from route
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string City { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public string Country { get; set; } = default!;
    public bool IsPrimary { get; set; }
}

public sealed class AddAddressValidator : Validator<AddAddressEndpointRequest>
{
    public AddAddressValidator()
    {
        RuleFor(x => x.Line1)
            .NotEmpty().WithMessage("Line1 is required.")
            .MaximumLength(200).WithMessage("Line1 must not exceed 200 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("PostalCode is required.")
            .MaximumLength(20).WithMessage("PostalCode must not exceed 20 characters.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters.");
    }
}

/// <summary>POST /api/v1/customers/{id}/addresses — User or Admin role required.</summary>
public sealed class AddAddress : Endpoint<AddAddressEndpointRequest, AddressDto>
{
    private readonly IAddressService _service;

    public AddAddress(IAddressService service) => _service = service;

    public override void Configure()
    {
        Post("api/v1/customers/{id}/addresses"); // {{TEMPLATE: RoutePrefixes}}
        Roles("user", "admin"); // {{TEMPLATE: PolicyNames}}
    }

    public override async Task HandleAsync(AddAddressEndpointRequest req, CancellationToken ct)
    {
        var appRequest = new AddAddressRequest(
            req.Line1, req.Line2, req.City, req.PostalCode, req.Country, req.IsPrimary);

        var result = await _service.AddAddressAsync(req.Id, appRequest, ct);
        if (result.IsSuccess)
        {
            HttpContext.Response.Headers.Location =
                $"/api/v1/customers/{req.Id}/addresses/{result.Value.Id}";
            await Send.ResponseAsync(result.Value, StatusCodes.Status201Created, ct);
        }
        else
        {
            await this.HandleErrorAsync(result.Error, ct);
        }
    }
}
