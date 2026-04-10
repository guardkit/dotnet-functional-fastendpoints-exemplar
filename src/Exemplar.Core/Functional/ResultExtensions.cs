using Exemplar.Core.Errors;

namespace Exemplar.Core.Functional;

public static class ResultExtensions
{
    public static async Task<Result<TError, TNew>> MapAsync<TError, TSuccess, TNew>(
        this Task<Result<TError, TSuccess>> resultTask,
        Func<TSuccess, TNew> mapper)
        where TError : BaseError
    {
        var result = await resultTask;
        return result.Map(mapper);
    }

    public static async Task<Result<TError, TNew>> BindAsync<TError, TSuccess, TNew>(
        this Task<Result<TError, TSuccess>> resultTask,
        Func<TSuccess, Task<Result<TError, TNew>>> binder)
        where TError : BaseError
    {
        var result = await resultTask;
        return await result.BindAsync(binder);
    }

    /// <summary>
    /// Transforms the error type while preserving the success value.
    /// Used to adapt cross-BC result types (e.g. NotFoundError → CustomerNotFoundError).
    /// </summary>
    public static async Task<Result<TNewError, TSuccess>> MapErrorAsync<TError, TSuccess, TNewError>(
        this Task<Result<TError, TSuccess>> resultTask,
        Func<TError, TNewError> errorMapper)
        where TError : BaseError
        where TNewError : BaseError
    {
        var result = await resultTask;
        return result.IsSuccess
            ? Result<TNewError, TSuccess>.Success(result.Value)
            : Result<TNewError, TSuccess>.Failure(errorMapper(result.Error));
    }
}
