using System.Diagnostics.CodeAnalysis;

namespace WhereToEat.SharedKernel.Results;

/// <summary>
/// The outcome of an operation that can fail without throwing: either a success or a
/// failure carrying an <see cref="Error"/>. Domain factories/guards return this instead
/// of throwing for expected, recoverable failures.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // A success must carry no error; a failure must carry one. Enforcing this here
        // keeps every Result internally consistent regardless of how it was constructed.
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>True when the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>True when the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The failure error, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    /// <summary>Creates a failed <see cref="Result{TValue}"/> carrying <paramref name="error"/>.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

/// <summary>
/// A <see cref="Result"/> that carries a <typeparamref name="TValue"/> on success.
/// Accessing <see cref="Value"/> on a failed result throws — callers must check
/// <see cref="Result.IsSuccess"/> first.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue _value;

    private Result(TValue value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The success value. Throws <see cref="InvalidOperationException"/> if the result is
    /// a failure — the failure branch never returns, so the value is always non-null when
    /// it is returned (no <c>[MaybeNull]</c> / <c>!</c> needed at call sites).
    /// </summary>
    public TValue Value =>
        IsSuccess
            ? _value
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Static factory methods are the idiomatic, type-inferred constructors of the Result pattern.")]
    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Static factory methods are the idiomatic, type-inferred constructors of the Result pattern.")]
    public static new Result<TValue> Failure(Error error) => new(default!, false, error);

    /// <summary>Implicitly lifts a value into a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly lifts an error into a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
