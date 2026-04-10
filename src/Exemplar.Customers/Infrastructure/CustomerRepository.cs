using Dapper;
using Exemplar.Core.Functional;
using Exemplar.Customers.Domain;
using Exemplar.Customers.Domain.Errors;
using Npgsql;

namespace Exemplar.Customers.Infrastructure;

/// <summary>
/// Dapper + Npgsql repository. Every public method wraps exceptions in
/// CustomerError.RepositoryUnavailable — callers never see raw exceptions.
/// </summary>
public sealed class CustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    static CustomerRepository()
    {
        // Map snake_case column names (e.g. created_at) to PascalCase properties (CreatedAt)
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public CustomerRepository(string connectionString)
        => _connectionString = connectionString;

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Result<CustomerError, Customer>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var customer = await conn.QuerySingleOrDefaultAsync<Customer>(
                "SELECT id, name, email, status, created_at FROM customers WHERE id = @Id",
                new { Id = id });
            return customer is null
                ? CustomerError.NotFound(id)
                : Result<CustomerError, Customer>.Success(customer);
        }
        catch (Exception ex)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }

    public async Task<Result<CustomerError, IReadOnlyList<Customer>>> GetAllAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var customers = await conn.QueryAsync<Customer>(
                "SELECT id, name, email, status, created_at FROM customers ORDER BY created_at DESC");
            return Result<CustomerError, IReadOnlyList<Customer>>.Success(customers.ToList());
        }
        catch (Exception ex)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }

    public async Task<Result<CustomerError, Customer>> InsertAsync(Customer customer, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var inserted = await conn.QuerySingleAsync<Customer>(
                """
                INSERT INTO customers (id, name, email, status, created_at)
                VALUES (@Id, @Name, @Email, @Status, @CreatedAt)
                RETURNING id, name, email, status, created_at
                """,
                new { customer.Id, customer.Name, customer.Email, Status = (int)customer.Status, customer.CreatedAt });
            return Result<CustomerError, Customer>.Success(inserted);
        }
        catch (Exception ex)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }

    public async Task<Result<CustomerError, Customer>> UpdateAsync(Customer customer, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var updated = await conn.QuerySingleOrDefaultAsync<Customer>(
                """
                UPDATE customers SET name = @Name, email = @Email, status = @Status
                WHERE id = @Id
                RETURNING id, name, email, status, created_at
                """,
                new { customer.Id, customer.Name, customer.Email, Status = (int)customer.Status });
            return updated is null
                ? CustomerError.NotFound(customer.Id)
                : Result<CustomerError, Customer>.Success(updated);
        }
        catch (Exception ex)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }

    public async Task<Result<CustomerError, bool>> EmailExistsAsync(string email, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM customers WHERE email = @Email)",
                new { Email = email });
            return Result<CustomerError, bool>.Success(exists);
        }
        catch (Exception ex)
        {
            return CustomerError.RepositoryUnavailable(ex);
        }
    }
}
