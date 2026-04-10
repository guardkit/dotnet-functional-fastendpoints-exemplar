using Microsoft.AspNetCore.Http;

namespace Exemplar.Core.Errors;

public abstract record BaseError(string Message)
{
    public virtual int StatusCode => StatusCodes.Status500InternalServerError;
    public string? ErrorCode { get; init; }
    public Exception? InnerException { get; init; }
}
