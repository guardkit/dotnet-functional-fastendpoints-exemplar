namespace Exemplar.Customers.Domain;

/// <summary>
/// Customer aggregate root. Public setters allow Dapper to hydrate from the database.
/// Use the static factory methods (Create, Deactivate) for domain-driven mutations.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public CustomerStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public static Customer Create(string name, string email) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        Status = CustomerStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    public Customer Deactivate() => new()
    {
        Id = Id,
        Name = Name,
        Email = Email,
        Status = CustomerStatus.Inactive,
        CreatedAt = CreatedAt
    };
}
