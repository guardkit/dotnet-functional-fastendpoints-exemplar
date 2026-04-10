using Exemplar.Core.Errors;
using Exemplar.Core.Functional;

namespace Exemplar.Customers.Contracts;

/// <summary>
/// Inter-BC contract used by other bounded contexts (e.g. Addresses) to look up customers.
/// Returns a lightweight summary DTO — callers never depend on the internal Customers BC types.
/// </summary>
public interface ICustomerLookup
{
    Task<Result<NotFoundError, CustomerSummaryDto>> FindByIdAsync(Guid id, CancellationToken ct);
}
