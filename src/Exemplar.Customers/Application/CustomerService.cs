using Exemplar.Core.Errors;
using Exemplar.Core.Functional;
using Exemplar.Customers.Contracts;
using Exemplar.Customers.Domain;
using Exemplar.Customers.Domain.Errors;
using Exemplar.Customers.Infrastructure;

namespace Exemplar.Customers.Application;

/// <summary>
/// Implements both ICustomerService (intra-BC) and ICustomerLookup (inter-BC).
/// All pipelines use .Bind()/.BindAsync() chains — no mid-chain .IsSuccess checks.
/// </summary>
public sealed class CustomerService : ICustomerService, ICustomerLookup
{
    private readonly ICustomerRepository _repo;

    public CustomerService(ICustomerRepository repo) => _repo = repo;

    public async Task<Result<CustomerError, CustomerDto>> GetByIdAsync(Guid id, CancellationToken ct)
        => (await _repo.GetByIdAsync(id, ct)).Map(ToDto);

    public async Task<Result<CustomerError, IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken ct)
        => (await _repo.GetAllAsync(ct))
           .Map(customers => (IReadOnlyList<CustomerDto>)customers.Select(ToDto).ToList());

    public Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(
        CreateCustomerRequest request, CancellationToken ct)
        => CheckEmailNotTakenAsync(request.Email, ct)
           .BindAsync(email => _repo.InsertAsync(Customer.Create(request.Name, email), ct))
           .MapAsync(ToDto);

    public Task<Result<CustomerError, CustomerDto>> DeactivateCustomerAsync(Guid id, CancellationToken ct)
        => _repo.GetByIdAsync(id, ct)
           .BindAsync(ValidateActiveStatus)
           .BindAsync(customer => _repo.UpdateAsync(customer.Deactivate(), ct))
           .MapAsync(ToDto);

    // ── ICustomerLookup (inter-BC) ─────────────────────────────────────────────

    public async Task<Result<NotFoundError, CustomerSummaryDto>> FindByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _repo.GetByIdAsync(id, ct);
        return result.Match<Result<NotFoundError, CustomerSummaryDto>>(
            onSuccess: c => new CustomerSummaryDto(c.Id, c.Name, c.Email),
            onFailure: _ => new NotFoundError($"Customer {id} not found"));
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private Task<Result<CustomerError, string>> CheckEmailNotTakenAsync(string email, CancellationToken ct)
        => _repo.EmailExistsAsync(email, ct)
           .BindAsync(exists => Task.FromResult(exists
               ? (Result<CustomerError, string>)CustomerError.EmailAlreadyExists(email)
               : Result<CustomerError, string>.Success(email)));

    private static Task<Result<CustomerError, Customer>> ValidateActiveStatus(Customer customer)
        => Task.FromResult(customer.Status == CustomerStatus.Inactive
            ? (Result<CustomerError, Customer>)CustomerError.AlreadyInactive(customer.Id)
            : Result<CustomerError, Customer>.Success(customer));

    private static CustomerDto ToDto(Customer c)
        => new(c.Id, c.Name, c.Email, c.Status.ToString(), c.CreatedAt);
}
