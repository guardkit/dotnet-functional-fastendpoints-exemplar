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
}
