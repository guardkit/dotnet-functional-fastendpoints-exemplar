using Microsoft.AspNetCore.Http;

namespace Exemplar.Core.Errors;

public abstract record BaseError(string Message)
{
    public virtual int StatusCode => StatusCodes.Status500InternalServerError;
    public virtual string? ErrorCode => null;
    public Exception? InnerException { get; init; }
}
