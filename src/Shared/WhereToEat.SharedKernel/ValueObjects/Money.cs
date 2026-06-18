using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.SharedKernel.ValueObjects;

/// <summary>
/// A monetary amount in a specific ISO 4217 currency. Equality is by value (amount +
/// currency). Arithmetic across different currencies is forbidden, and negative amounts
/// are rejected at construction — prices in this system are never negative (Step 1 / the
/// MenuItem price guard in later steps relies on this).
/// </summary>
public sealed class Money : ValueObject
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount; always non-negative for a constructed <see cref="Money"/>.</summary>
    public decimal Amount { get; }

    /// <summary>The uppercase ISO 4217 currency code (e.g. "UAH", "USD").</summary>
    public string Currency { get; }

    /// <summary>
    /// Creates a <see cref="Money"/>, rejecting a negative amount or a malformed currency
    /// code (must be three letters). Returns a failure <see cref="Result"/> instead of
    /// throwing because invalid amounts/currencies are expected, recoverable input.
    /// </summary>
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0m)
        {
            return Result.Failure<Money>(
                Error.Validation("Money.NegativeAmount", "A monetary amount cannot be negative."));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3 || !IsAsciiLetters(currency.Trim()))
        {
            return Result.Failure<Money>(
                Error.Validation("Money.InvalidCurrency", "Currency must be a 3-letter ISO 4217 code."));
        }

        return Result.Success(new Money(amount, currency.Trim().ToUpperInvariant()));
    }

    /// <summary>A zero amount in the given currency (a convenience for accumulation).</summary>
    public static Result<Money> Zero(string currency) => Create(0m, currency);

    /// <summary>Adds two amounts of the same currency.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts an amount of the same currency. Throws if the result would go negative —
    /// negative money is not representable in this model.
    /// </summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0m)
        {
            throw new InvalidOperationException("Subtracting would produce a negative monetary amount.");
        }

        return new Money(result, Currency);
    }

    public static Money operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Add(right);
    }

    public static Money operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Subtract(right);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Currency}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Mixing currencies in arithmetic is a programming error, not recoverable input,
        // so we throw rather than return a Result here.
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot operate on Money of different currencies ('{Currency}' vs '{other.Currency}').");
        }
    }

    // ISO 4217 codes are ASCII A–Z only. char.IsLetter would also accept Unicode letters
    // (é, à, ñ, Cyrillic, …), which are never valid currency codes — so this guard is
    // deliberately ASCII-restricted.
    private static bool IsAsciiLetters(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiLetter(ch))
            {
                return false;
            }
        }

        return true;
    }
}
