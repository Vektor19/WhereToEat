using WhereToEat.Catalog.Domain.Identifiers;
using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Catalog.Domain.Photos;

/// <summary>
/// A photo shown for a dish (invariant #8). Two kinds exist, gated so the rules can never be bypassed:
/// <list type="bullet">
///   <item>a <b>generic</b> category photo — <b>our own content</b>, the <b>default on every dish</b>,
///   always displayable; and</item>
///   <item>a <b>real</b> photo of the venue's actual dish — only ever sourced from a venue that gave
///   permission, and <b>not displayable</b> unless <see cref="PermissionGranted"/> is <c>true</c>.</item>
/// </list>
///
/// <para>
/// The gate is the single method <see cref="CanBeDisplayed"/>: a generic photo is always displayable;
/// a real photo is displayable only when permission is granted. <see cref="Generic"/> creates the
/// default; <see cref="RealWithPermission"/> is the only way to obtain a displayable real photo, and
/// <see cref="RevokePermission"/> flips it back to non-displayable when a venue's permission ends.
/// </para>
/// </summary>
public sealed class Photo : Entity<PhotoId>
{
    private Photo(PhotoId id, DishId dish, bool isGeneric, bool permissionGranted, string url)
        : base(id)
    {
        Dish = dish;
        IsGeneric = isGeneric;
        PermissionGranted = permissionGranted;
        Url = url;
    }

    /// <summary>The dish this photo illustrates.</summary>
    public DishId Dish { get; }

    /// <summary>
    /// True when this is our own generic category illustration (the default on every dish) rather than
    /// a venue's real photo. A generic photo is always displayable.
    /// </summary>
    public bool IsGeneric { get; }

    /// <summary>
    /// True only when the venue has granted permission to show its <b>real</b> photo. Always
    /// irrelevant for a generic photo (which is our own content); for a real photo this is the gate.
    /// </summary>
    public bool PermissionGranted { get; private set; }

    /// <summary>The image URL (our asset for generic; the venue-provided asset for a real photo).</summary>
    public string Url { get; }

    /// <summary>
    /// Creates the default <b>generic</b> category photo for a dish — our own content, always
    /// displayable. This is what stands on every dish until a venue's real photo is permitted.
    /// </summary>
    public static Result<Photo> Generic(DishId dish, string url)
        => Generic(PhotoId.New(), dish, url);

    /// <summary>Creates a generic photo with an explicit id (for rehydration/seeding).</summary>
    public static Result<Photo> Generic(PhotoId id, DishId dish, string url)
    {
        var validation = ValidateUrl(url);
        if (validation.IsFailure)
        {
            return Result.Failure<Photo>(validation.Error);
        }

        // permissionGranted is irrelevant for a generic photo (our own content) — kept false.
        return Result.Success(new Photo(id, dish, isGeneric: true, permissionGranted: false, url: url.Trim()));
    }

    /// <summary>
    /// Creates a <b>real</b> venue photo that the venue has <b>permitted</b> us to show. This is the
    /// only factory that yields a displayable real photo (invariant #8) — there is intentionally no
    /// path that produces a displayable real photo without permission.
    /// </summary>
    public static Result<Photo> RealWithPermission(DishId dish, string url)
        => RealWithPermission(PhotoId.New(), dish, url);

    /// <summary>Creates a permitted real photo with an explicit id (for rehydration/seeding).</summary>
    public static Result<Photo> RealWithPermission(PhotoId id, DishId dish, string url)
    {
        var validation = ValidateUrl(url);
        if (validation.IsFailure)
        {
            return Result.Failure<Photo>(validation.Error);
        }

        return Result.Success(new Photo(id, dish, isGeneric: false, permissionGranted: true, url: url.Trim()));
    }

    /// <summary>
    /// Rehydrates a photo from its persisted flags. Unlike <see cref="Generic(PhotoId, DishId, string)"/>
    /// and <see cref="RealWithPermission(PhotoId, DishId, string)"/>, this accepts the stored
    /// <paramref name="isGeneric"/>/<paramref name="permissionGranted"/> as-is, so it can reconstruct the
    /// <b>real-but-revoked</b> state (generic = false, permission = false) that <see cref="RevokePermission"/>
    /// produces — a state the schema persists but the two creation factories cannot express. It still
    /// enforces the same guard as the <c>CK_Photo_GenericHasNoPermission</c> constraint: a generic photo
    /// (our own content) must never carry a granted permission. Internal — persistence-only.
    /// </summary>
    internal static Result<Photo> Rehydrate(PhotoId id, DishId dish, bool isGeneric, bool permissionGranted, string url)
    {
        var validation = ValidateUrl(url);
        if (validation.IsFailure)
        {
            return Result.Failure<Photo>(validation.Error);
        }

        if (isGeneric && permissionGranted)
        {
            // Mirrors CK_Photo_GenericHasNoPermission: generic content is ours and never venue-permitted.
            return Result.Failure<Photo>(Error.Validation(
                "Photo.GenericHasNoPermission", "A generic photo must not carry a granted permission."));
        }

        return Result.Success(new Photo(id, dish, isGeneric, permissionGranted, url.Trim()));
    }

    /// <summary>
    /// Whether this photo may be displayed to users. A generic photo always may (our content); a real
    /// photo may only when <see cref="PermissionGranted"/> is true. This is the invariant-#8 gate.
    /// </summary>
    public bool CanBeDisplayed() => IsGeneric || PermissionGranted;

    /// <summary>
    /// Revokes a real photo's display permission (e.g. the venue ended its agreement), making it
    /// non-displayable again. A no-op on a generic photo, which is always our own displayable content.
    /// </summary>
    public void RevokePermission()
    {
        if (!IsGeneric)
        {
            PermissionGranted = false;
        }
    }

    private static Result ValidateUrl(string url)
        => string.IsNullOrWhiteSpace(url)
            ? Result.Failure(Error.Validation("Photo.UrlRequired", "A photo must have a non-empty URL."))
            : Result.Success();
}
