namespace Exemplar.Addresses.Application;

public record AddressDto(
    Guid Id,
    Guid CustomerId,
    string Line1,
    string? Line2,
    string City,
    string PostalCode,
    string Country,
    bool IsPrimary,
    DateTime CreatedAt);
