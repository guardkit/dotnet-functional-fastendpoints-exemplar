using Exemplar.Core.Errors;
using Exemplar.Core.Functional;
using Exemplar.Customers.Contracts;
using Exemplar.Customers.Domain;
using Exemplar.Customers.Domain.Errors;
using Exemplar.Customers.Infrastructure;
using Exemplar.Fleet.Models;
using Exemplar.Fleet.Services;

namespace Exemplar.Customers.Application;

/// <summary>
/// Implements both ICustomerService (intra-BC) and ICustomerLookup (inter-BC).
/// All pipelines use .Bind()/.BindAsync() chains — no mid-chain .IsSuccess checks.
///
/// Fleet integration (W7):
///   - INatsEventPublisher is optional; omit to disable domain-event publishing.
///   - INatsAgentClient is optional; omit to disable agent enrichment calls.
/// </summary>
public sealed class CustomerService : ICustomerService, ICustomerLookup
{
    private readonly ICustomerRepository _repo;
    private readonly INatsEventPublisher? _eventPublisher;
    private readonly INatsAgentClient? _agentClient;

    public CustomerService(
        ICustomerRepository repo,
        INatsEventPublisher? eventPublisher = null,
        INatsAgentClient? agentClient = null)
    {
        _repo = repo;
        _eventPublisher = eventPublisher;
        _agentClient = agentClient;
    }

    public async Task<Result<CustomerError, CustomerDto>> GetByIdAsync(Guid id, CancellationToken ct)
        => (await _repo.GetByIdAsync(id, ct)).Map(ToDto);

    public async Task<Result<CustomerError, IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken ct)
        => (await _repo.GetAllAsync(ct))
           .Map(customers => (IReadOnlyList<CustomerDto>)customers.Select(ToDto).ToList());

    public Task<Result<CustomerError, CustomerDto>> CreateCustomerAsync(
        CreateCustomerRequest request, CancellationToken ct)
        => CheckEmailNotTakenAsync(request.Email, ct)
           .BindAsync(email => _repo.InsertAsync(Customer.Create(request.Name, email), ct))
           .MapAsync(ToDto)
           .TapAsync(dto => PublishCustomerCreatedAsync(dto, ct));

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

    // ── Agent enrichment (W7 demo) ─────────────────────────────────────────────

    /// <summary>
    /// Demonstrates request-reply with <see cref="INatsAgentClient"/>.
    /// Attempts to call a "customer_enrichment" agent tool; returns the plain DTO
    /// if no agent is registered (graceful degradation).
    /// </summary>
    public async Task<Result<CustomerError, CustomerDto>> GetEnrichedAsync(Guid id, CancellationToken ct)
    {
        var customerResult = await GetByIdAsync(id, ct);
        if (customerResult.IsFailure || _agentClient is null)
            return customerResult;

        var dto = customerResult.Value;
        var enrichment = await _agentClient.RequestAsync<CustomerEnrichmentResult>(
            toolName: "customer_enrichment",
            command: "enrich",
            args: new { dto.Name, dto.Email },
            ct: ct);

        // Agent is best-effort — return plain customer if unavailable or call failed.
        return customerResult;
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

    private Task PublishCustomerCreatedAsync(CustomerDto dto, CancellationToken ct)
    {
        if (_eventPublisher is null) return Task.CompletedTask;
        return _eventPublisher.PublishAsync(
            subject: "customers.created",
            eventType: EventType.Event,
            payload: new { CustomerId = dto.Id, dto.Name, dto.Email },
            ct: ct);
    }

    private static CustomerDto ToDto(Customer c)
        => new(c.Id, c.Name, c.Email, c.Status.ToString(), c.CreatedAt);

    // ── Nested types ───────────────────────────────────────────────────────────

    /// <summary>Placeholder result type for the agent enrichment demo.</summary>
    public sealed record CustomerEnrichmentResult(string? Segment, string? RiskScore);
}
