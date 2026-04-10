namespace Exemplar.Addresses.Application;

public record AddAddressRequest(
    string Line1,
    string? Line2,
    string City,
    string PostalCode,
    string Country,
    bool IsPrimary);
