namespace WhereToEat.Parsing.Domain;

/// <summary>
/// The restaurant-level facts a parser extracted from a first-party source: the display
/// <see cref="Name"/>, the postal <see cref="AddressLine"/> + optional <see cref="City"/> (parsed,
/// later admin-editable and geocoded), and the always-shown/never-monetized site/social
/// <see cref="ContactLinks"/> (invariant #10). These are the header of the normalized
/// <see cref="ParsedMenu"/> contract both strategy kinds must produce identically.
/// </summary>
public sealed record ParsedRestaurantFacts(
    string Name,
    string AddressLine,
    string? City,
    IReadOnlyList<ParsedContactLink> ContactLinks);

/// <summary>The kind of a parsed contact link, mirroring the catalog's contact-link kinds.</summary>
public enum ParsedContactLinkKind
{
    /// <summary>The venue's own website.</summary>
    Website = 0,

    /// <summary>A social-media profile.</summary>
    Social = 1,

    /// <summary>A phone number presented as a link.</summary>
    Phone = 2,
}

/// <summary>A site/social/phone link a parser found, before persistence.</summary>
public sealed record ParsedContactLink(ParsedContactLinkKind Kind, string Url, string? Label = null);
