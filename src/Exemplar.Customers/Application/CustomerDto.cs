namespace Exemplar.Customers.Application;

public record CustomerDto(Guid Id, string Name, string Email, string Status, DateTime CreatedAt);
