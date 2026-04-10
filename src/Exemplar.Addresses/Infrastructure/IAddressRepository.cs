using Exemplar.Addresses.Domain;
using Exemplar.Core.Errors;
using Exemplar.Core.Functional;

namespace Exemplar.Addresses.Infrastructure;

public interface IAddressRepository
{
    Task<Result<BaseError, IReadOnlyList<Address>>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken ct);

    Task<Result<BaseError, Address>> InsertAsync(
        Address address, CancellationToken ct);

    /// <summary>Returns true if the customer already has a primary address.</summary>
    Task<Result<BaseError, bool>> HasPrimaryAddressAsync(
        Guid customerId, CancellationToken ct);
}
