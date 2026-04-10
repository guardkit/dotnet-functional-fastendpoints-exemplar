using Microsoft.AspNetCore.Http;

namespace Exemplar.Core.Errors;

public sealed record NotFoundError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed record ValidationError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}

public sealed record UnauthorizedError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}

public sealed record ForbiddenError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

public sealed record ConflictError(string Message) : BaseError(Message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public sealed record InternalError(string Message) : BaseError(Message);
