using Exemplar.Addresses.Application;
using Exemplar.Core.Endpoints;
using FastEndpoints;

namespace Exemplar.Addresses.Endpoints;

public sealed class GetAddressesByCustomerIdRequest
{
    public Guid Id { get; set; }
}

/// <summary>GET /api/v1/customers/{id}/addresses — requires authenticated user.</summary>
public sealed class GetAddressesByCustomerId
    : Endpoint<GetAddressesByCustomerIdRequest, IReadOnlyList<AddressDto>>
{
    private readonly IAddressService _service;

    public GetAddressesByCustomerId(IAddressService service) => _service = service;

    public override void Configure()
    {
        Get("api/v1/customers/{id}/addresses");
        // No AllowAnonymous() — the global default policy (RequireAuthenticatedUser) enforces auth.
    }

    public override async Task HandleAsync(GetAddressesByCustomerIdRequest req, CancellationToken ct)
    {
        var result = await _service.GetByCustomerIdAsync(req.Id, ct);
        if (result.IsSuccess)
            await Send.OkAsync(result.Value, ct);
        else
            await this.HandleErrorAsync(result.Error, ct);
    }
}
