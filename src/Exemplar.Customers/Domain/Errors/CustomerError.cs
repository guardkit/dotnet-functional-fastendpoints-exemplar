using Exemplar.Core.Errors;
using Microsoft.AspNetCore.Http;

namespace Exemplar.Customers.Domain.Errors;

/// <summary>
/// Pattern A: enum-discriminated error with static factories.
/// StatusCode is derived from Kind via a switch expression.
/// </summary>
public record CustomerError(string Message, CustomerErrorKind Kind) : BaseError(Message)
{
    public override int StatusCode => Kind switch
    {
        CustomerErrorKind.NotFound            => StatusCodes.Status404NotFound,
        CustomerErrorKind.EmailAlreadyExists  => StatusCodes.Status409Conflict,
        CustomerErrorKind.AlreadyInactive     => StatusCodes.Status409Conflict,
        _                                     => StatusCodes.Status500InternalServerError
    };

    public override string? ErrorCode => Kind switch
    {
        CustomerErrorKind.NotFound            => "CUSTOMER_NOT_FOUND",
        CustomerErrorKind.EmailAlreadyExists  => "CUSTOMER_EMAIL_EXISTS",
        CustomerErrorKind.AlreadyInactive     => "CUSTOMER_ALREADY_INACTIVE",
        _                                     => null
    };

    public static CustomerError NotFound(Guid id)
        => new($"Customer {id} not found", CustomerErrorKind.NotFound);

    public static CustomerError EmailAlreadyExists(string email)
        => new($"Email {email} is already registered", CustomerErrorKind.EmailAlreadyExists);

    public static CustomerError AlreadyInactive(Guid id)
        => new($"Customer {id} is already inactive", CustomerErrorKind.AlreadyInactive);

    public static CustomerError RepositoryUnavailable(Exception ex)
        => new("Customer data store unavailable", CustomerErrorKind.RepositoryUnavailable)
           { InnerException = ex };
}
