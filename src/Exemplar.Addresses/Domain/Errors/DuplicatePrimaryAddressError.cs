using Exemplar.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Addresses.Domain.Errors;

/// <summary>Raised when attempting to add a second primary address for the same customer.</summary>
public record DuplicatePrimaryAddressError(Guid CustomerId)
    : BaseError($"Customer {CustomerId} already has a primary address")
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string? ErrorCode => "ADDRESS_DUPLICATE_PRIMARY";
}
