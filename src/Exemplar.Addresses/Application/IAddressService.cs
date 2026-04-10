using Exemplar.Core.Errors;
using Exemplar.Core.Functional;

namespace Exemplar.Addresses.Application;

/// <summary>Public API surface for the Addresses bounded context.</summary>
public interface IAddressService
{
    Task<Result<BaseError, IReadOnlyList<AddressDto>>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken ct);

    Task<Result<BaseError, AddressDto>> AddAddressAsync(
        Guid customerId, AddAddressRequest request, CancellationToken ct);
}
