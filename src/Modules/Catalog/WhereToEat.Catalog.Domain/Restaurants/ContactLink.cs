using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// The kind of contact link a restaurant exposes. Open-ended enough for the links the design
/// surfaces in restaurant details; new kinds can be added without touching call sites.
/// </summary>
public enum ContactLinkKind
{
    /// <summary>The venue's own website.</summary>
    Website = 0,

    /// <summary>A social-media profile (Instagram, Facebook, …).</summary>
    Social = 1,

    /// <summary>A phone number presented as a link.</summary>
    Phone = 2,
}

/// <summary>
/// A website/social link shown on a restaurant's details (a value object). Per invariant #10 these
/// links are <b>always shown for free</b> in the details, even for non-Verified venues, and are
/// <b>never monetized</b> — so this type carries no "paid"/"promoted" flag by design. Equality is
/// by kind + URL so the same link is never stored twice on a restaurant.
/// </summary>
public sealed class ContactLink : ValueObject
{
    private ContactLink(ContactLinkKind kind, string url, string? label)
    {
        Kind = kind;
        Url = url;
        Label = label;
    }

    /// <summary>What sort of link this is (website / social / phone).</summary>
    public ContactLinkKind Kind { get; }

    /// <summary>The link target (an http/https URL, or a tel: value for a phone). Non-blank.</summary>
    public string Url { get; }

    /// <summary>An optional display label (e.g. "Instagram"); otherwise <c>null</c>.</summary>
    public string? Label { get; }

    /// <summary>
    /// Creates a contact link, rejecting a blank URL. A blank <paramref name="label"/> is
    /// normalised to <c>null</c> so "no label" is represented uniformly.
    /// </summary>
    public static Result<ContactLink> Create(ContactLinkKind kind, string url, string? label = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Failure<ContactLink>(
                Error.Validation("ContactLink.UrlRequired", "A contact link URL cannot be blank."));
        }

        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        return Result.Success(new ContactLink(kind, url.Trim(), normalizedLabel));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Url;
    }
}
