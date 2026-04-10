namespace Exemplar.Customers.Domain.Errors;

public enum CustomerErrorKind
{
    NotFound,
    EmailAlreadyExists,
    AlreadyInactive,
    RepositoryUnavailable
}
