using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Analytics.Domain.Anonymization;

/// <summary>
/// A <b>rotating-salted hash</b> of a user/session id — the "hash" rule of the anonymization shape
/// (§8.1 / invariant #11). The raw id is never stored; only this opaque digest is, and because the
/// salt rotates on a schedule the same user hashes <b>differently across windows</b> (not
/// cross-window linkable) yet <b>stably within a window</b> (intra-window funnel math works).
///
/// <para>
/// This value object is the domain <i>shape</i> — it holds the already-computed digest and refuses a
/// raw-looking value. The actual salting/hashing and salt rotation live in the Step 11 pipeline
/// (<c>IAnonymizer</c> / <c>ISaltProvider</c>); this type only guarantees what is stored is a hash.
/// Equality is by value.
/// </para>
/// </summary>
public sealed class HashedActorId : ValueObject
{
    /// <summary>The fixed length of a stored digest (hex-encoded SHA-256 = 64 chars).</summary>
    public const int DigestLength = 64;

    private HashedActorId(string digest)
    {
        Digest = digest;
    }

    /// <summary>The opaque, non-reversible hex digest of the salted actor id.</summary>
    public string Digest { get; }

    /// <summary>
    /// Wraps an already-computed digest, rejecting an empty value, a value of the wrong length, or one
    /// containing non-hex characters (a raw id would never satisfy these — so a raw value can't slip
    /// through as if it were a hash).
    /// </summary>
    public static Result<HashedActorId> FromDigest(string digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return Result.Failure<HashedActorId>(
                Error.Validation("HashedActorId.Required", "A hashed actor id digest is required."));
        }

        var normalized = digest.Trim().ToLowerInvariant();

        if (normalized.Length != DigestLength)
        {
            return Result.Failure<HashedActorId>(
                Error.Validation(
                    "HashedActorId.WrongLength",
                    $"A hashed actor id must be a {DigestLength}-char hex digest (SHA-256)."));
        }

        foreach (var ch in normalized)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return Result.Failure<HashedActorId>(
                    Error.Validation("HashedActorId.NotHex", "A hashed actor id must be hex (the raw id is never stored)."));
            }
        }

        return Result.Success(new HashedActorId(normalized));
    }

    /// <inheritdoc />
    public override string ToString() => Digest;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Digest;
    }
}
