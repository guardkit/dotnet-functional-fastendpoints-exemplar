using Exemplar.Core.Functional;
using Exemplar.Customers.Domain.Errors;

namespace Exemplar.Customers.Application;

public interface ICustomerService
{
    Task<Result<CustomerError, CustomerDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<CustomerError, IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken ct);
    Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct);
    Task<Result<CustomerError, CustomerDto>> DeactivateCustomerAsync(Guid id, CancellationToken ct);
}
