using Exemplar.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Addresses.Domain.Errors;

/// <summary>
/// Raised when the Addresses BC attempts to look up a customer that does not exist.
/// Distinct from Core.NotFoundError so callers can distinguish which resource was missing.
/// </summary>
public record CustomerNotFoundError(Guid CustomerId)
    : BaseError($"Customer {CustomerId} not found")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string? ErrorCode => "ADDRESS_CUSTOMER_NOT_FOUND";
}
