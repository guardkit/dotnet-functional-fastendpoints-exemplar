using Exemplar.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Addresses.Domain.Errors;

/// <summary>Pattern B: each error is its own record type with its own StatusCode override.</summary>
public record AddressNotFoundError(Guid AddressId)
    : BaseError($"Address {AddressId} not found")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string? ErrorCode => "ADDRESS_NOT_FOUND";
}
