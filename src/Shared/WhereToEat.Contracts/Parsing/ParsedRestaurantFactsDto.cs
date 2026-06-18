namespace WhereToEat.Contracts.Parsing;

/// <summary>
/// The restaurant-level facts a parser extracted from a first-party source, in transport shape: the
/// display name, the postal address line + city (admin-editable, geocoded separately), and the
/// always-shown/never-monetized site/social contact links (invariant #10). This crosses the module
/// boundary as a plain DTO so the Parsing module never constructs a Catalog <c>Restaurant</c>
/// aggregate itself — the Catalog adapter rebuilds the aggregate from this on the persist side.
/// </summary>
public sealed record ParsedRestaurantFactsDto(
    string Name,
    string AddressLine,
    string? City,
    IReadOnlyList<ParsedContactLinkDto> ContactLinks);

/// <summary>
/// A site/social contact link a parser found, in transport shape. <see cref="Kind"/> mirrors the
/// catalog's contact-link kinds (0 = website, 1 = social, 2 = phone) as a plain int so Parsing carries
/// no Catalog enum; the Catalog adapter maps it back to its own value object.
/// </summary>
public sealed record ParsedContactLinkDto(int Kind, string Url, string? Label = null);
