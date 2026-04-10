namespace Exemplar.Addresses.Domain;

/// <summary>
/// Address aggregate root. Public setters allow Dapper to hydrate from the database.
/// Use the static factory method (Create) for domain-driven construction.
/// </summary>
public sealed class Address
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string City { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public string Country { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }

    public static Address Create(
        Guid customerId,
        string line1,
        string? line2,
        string city,
        string postalCode,
        string country,
        bool isPrimary) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        Line1 = line1,
        Line2 = line2,
        City = city,
        PostalCode = postalCode,
        Country = country,
        IsPrimary = isPrimary,
        CreatedAt = DateTime.UtcNow
    };
}
