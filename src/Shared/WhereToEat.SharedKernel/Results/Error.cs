using System.Diagnostics.CodeAnalysis;

namespace WhereToEat.SharedKernel.Results;

/// <summary>
/// Classifies the nature of an <see cref="Error"/> so callers (and later the API layer)
/// can map a failure to the right outcome without string-matching the code.
/// </summary>
public enum ErrorType
{
    /// <summary>A general failure with no more specific classification.</summary>
    Failure = 0,

    /// <summary>The input was syntactically/semantically invalid (e.g. out-of-range value).</summary>
    Validation = 1,

    /// <summary>A requested resource was not found.</summary>
    NotFound = 2,

    /// <summary>The operation conflicts with current state (e.g. a duplicate).</summary>
    Conflict = 3,
}

/// <summary>
/// An immutable description of why an operation failed: a stable machine-readable
/// <paramref name="Code"/>, a human-readable <paramref name="Message"/>, and a
/// <paramref name="Type"/> classification. Errors are values and compare by value.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' is the canonical name in the Result pattern; this is a C#-only backend.")]
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    /// <summary>The canonical "no error" sentinel carried by a successful result.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error.</summary>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates a general <see cref="ErrorType.Failure"/> error.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
