namespace WhereToEat.SharedKernel.Results;

/// <summary>
/// Functional helpers for composing <see cref="Result"/> / <see cref="Result{TValue}"/>
/// without unwrapping-and-rewrapping at every call site. Pure, no I/O.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Projects the value of a successful result through <paramref name="map"/>; a failure
    /// is propagated unchanged (its error is preserved).
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error);
    }

    /// <summary>
    /// Chains a result-producing step onto a successful result; a failure short-circuits
    /// and is propagated unchanged.
    /// </summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return result.IsSuccess ? bind(result.Value) : Result.Failure<TOut>(result.Error);
    }

    /// <summary>
    /// Runs <paramref name="onSuccess"/> or <paramref name="onFailure"/> depending on the
    /// outcome and returns the produced value — a total fold over the result.
    /// </summary>
    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }
}
