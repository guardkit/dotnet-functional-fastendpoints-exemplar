using Exemplar.Core.Functional;
using Exemplar.Customers.Domain;
using Exemplar.Customers.Domain.Errors;

namespace Exemplar.Customers.Infrastructure;

public interface ICustomerRepository
{
    Task<Result<CustomerError, Customer>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<CustomerError, IReadOnlyList<Customer>>> GetAllAsync(CancellationToken ct);
    Task<Result<CustomerError, Customer>> InsertAsync(Customer customer, CancellationToken ct);
    Task<Result<CustomerError, Customer>> UpdateAsync(Customer customer, CancellationToken ct);
    Task<Result<CustomerError, bool>> EmailExistsAsync(string email, CancellationToken ct);
}
