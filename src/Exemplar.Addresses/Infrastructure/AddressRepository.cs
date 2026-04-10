using Dapper;
using Exemplar.Addresses.Domain;
using Exemplar.Core.Errors;
using Exemplar.Core.Functional;
using Npgsql;

namespace Exemplar.Addresses.Infrastructure;

/// <summary>
/// Dapper + Npgsql repository. Every public method wraps exceptions in an InternalError
/// so callers never see raw exceptions.
/// </summary>
public sealed class AddressRepository : IAddressRepository
{
    private readonly string _connectionString;

    static AddressRepository()
    {
        // Map snake_case column names (e.g. customer_id, is_primary) to PascalCase properties.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public AddressRepository(string connectionString)
        => _connectionString = connectionString;

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<Result<BaseError, IReadOnlyList<Address>>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var addresses = await conn.QueryAsync<Address>(
                """
                SELECT id, customer_id, line1, line2, city, postal_code, country, is_primary, created_at
                FROM addresses
                WHERE customer_id = @CustomerId
                ORDER BY created_at DESC
                """,
                new { CustomerId = customerId });
            return Result<BaseError, IReadOnlyList<Address>>.Success(addresses.ToList());
        }
        catch (Exception ex)
        {
            return new InternalError($"Repository unavailable: {ex.Message}");
        }
    }

    public async Task<Result<BaseError, Address>> InsertAsync(Address address, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var inserted = await conn.QuerySingleAsync<Address>(
                """
                INSERT INTO addresses (id, customer_id, line1, line2, city, postal_code, country, is_primary, created_at)
                VALUES (@Id, @CustomerId, @Line1, @Line2, @City, @PostalCode, @Country, @IsPrimary, @CreatedAt)
                RETURNING id, customer_id, line1, line2, city, postal_code, country, is_primary, created_at
                """,
                new
                {
                    address.Id, address.CustomerId, address.Line1, address.Line2,
                    address.City, address.PostalCode, address.Country,
                    address.IsPrimary, address.CreatedAt
                });
            return Result<BaseError, Address>.Success(inserted);
        }
        catch (Exception ex)
        {
            return new InternalError($"Repository unavailable: {ex.Message}");
        }
    }

    public async Task<Result<BaseError, bool>> HasPrimaryAddressAsync(
        Guid customerId, CancellationToken ct)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM addresses WHERE customer_id = @CustomerId AND is_primary = true)",
                new { CustomerId = customerId });
            return Result<BaseError, bool>.Success(exists);
        }
        catch (Exception ex)
        {
            return new InternalError($"Repository unavailable: {ex.Message}");
        }
    }
}
