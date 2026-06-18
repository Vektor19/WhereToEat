using WhereToEat.SharedKernel.Primitives;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// A restaurant's postal address (a value object — equal by value, no identity). Parsed first
/// then admin-editable; when it changes on a <see cref="Restaurant"/> the aggregate raises an
/// <see cref="Events.AddressChanged"/> event so the geocoder can refresh coordinates (the
/// re-geocode wiring itself lives in the Geo module, Step 8). The address is deliberately a
/// single normalized line plus an optional city: geocoding (OSM/Nominatim) takes a free-form
/// string, so over-structuring it here would add no query value.
/// </summary>
public sealed class Address : ValueObject
{
    private Address(string line, string? city)
    {
        Line = line;
        City = city;
    }

    /// <summary>The street line used for geocoding (e.g. "вул. Хрещатик, 1"). Non-blank.</summary>
    public string Line { get; }

    /// <summary>The city/locality, when known; otherwise <c>null</c>.</summary>
    public string? City { get; }

    /// <summary>
    /// Creates an address, rejecting a blank street line. An empty <paramref name="city"/> is
    /// normalised to <c>null</c> (absent) rather than an empty string so equality and persistence
    /// treat "no city" uniformly.
    /// </summary>
    public static Result<Address> Create(string line, string? city = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Result.Failure<Address>(
                Error.Validation("Address.LineRequired", "An address line cannot be blank."));
        }

        var normalizedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        return Result.Success(new Address(line.Trim(), normalizedCity));
    }

    /// <inheritdoc />
    public override string ToString() => City is null ? Line : $"{Line}, {City}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line;
        yield return City;
    }
}
