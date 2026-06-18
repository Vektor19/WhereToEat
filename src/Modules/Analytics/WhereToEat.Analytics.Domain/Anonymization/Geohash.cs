using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Analytics.Domain.Anonymization;

/// <summary>
/// A <b>coarse</b> geohash — the only location data analytics retains (invariant #11: gео is
/// approximate and optional; precise lat/lng is <b>dropped</b> at ingest). A geohash is a short
/// lowercase base-32 string whose length sets its precision; this value object enforces a
/// <b>neighbourhood-grade</b> length cap so a stored hash can never be point-precise.
///
/// <para>
/// This is the domain <i>shape</i> (the retain rule's value) — the encoder that derives a geohash from
/// coordinates and the precision config live in the Step 11 ingest pipeline. Equality is by value.
/// </para>
/// </summary>
public sealed class Geohash : ValueObject
{
    /// <summary>The base-32 alphabet geohashes use (no a, i, l, o to avoid ambiguity).</summary>
    public const string Base32Alphabet = "0123456789bcdefghjkmnpqrstuvwxyz";

    /// <summary>
    /// The maximum stored geohash length. Cap chosen so the retained cell stays neighbourhood-grade
    /// (≈ ±0.6 km at length 6 / ±2.4 km at length 5), never point-precise — the privacy floor.
    /// </summary>
    public const int MaxPrecision = 6;

    private Geohash(string value)
    {
        Value = value;
    }

    /// <summary>The coarse geohash string (lowercase base-32, length 1..<see cref="MaxPrecision"/>).</summary>
    public string Value { get; }

    /// <summary>The precision (character count) of this geohash.</summary>
    public int Precision => Value.Length;

    /// <summary>
    /// Creates a coarse geohash, rejecting an empty value, a value longer than
    /// <see cref="MaxPrecision"/> (would be too precise — a privacy violation), or a value containing
    /// any character outside the base-32 alphabet.
    /// </summary>
    public static Result<Geohash> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Geohash>(
                Error.Validation("Geohash.Required", "A geohash value is required."));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxPrecision)
        {
            return Result.Failure<Geohash>(
                Error.Validation(
                    "Geohash.TooPrecise",
                    $"A retained geohash must be at most {MaxPrecision} chars (neighbourhood-grade); precise location is dropped."));
        }

        foreach (var ch in normalized)
        {
            if (!Base32Alphabet.Contains(ch, StringComparison.Ordinal))
            {
                return Result.Failure<Geohash>(
                    Error.Validation("Geohash.InvalidCharacter", $"'{ch}' is not a valid geohash base-32 character."));
            }
        }

        return Result.Success(new Geohash(normalized));
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
