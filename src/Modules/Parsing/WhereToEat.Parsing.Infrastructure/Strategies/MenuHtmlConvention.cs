using System.Globalization;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.ValueObjects;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// The shared menu-HTML convention both example fixtures follow, so the Selenium and AngleSharp
/// strategies extract the <b>same</b> normalized <see cref="ParsedMenu"/> contract from the same page
/// shape. A page carries restaurant facts on the <c>[data-restaurant]</c> root
/// (<c>data-name</c>/<c>data-address</c>/<c>data-city</c>) and contact links + dish rows as marked
/// elements. Keeping the parsing of the extracted fields in one place avoids duplicating the
/// raw-string → <see cref="Money"/> conversion across the two strategies (DRY): each strategy only
/// supplies the per-element raw strings it read with its own DOM API.
/// </summary>
internal static class MenuHtmlConvention
{
    /// <summary>CSS selector for the restaurant-facts root element.</summary>
    public const string RestaurantRootSelector = "[data-restaurant]";

    /// <summary>CSS selector for each contact-link element.</summary>
    public const string ContactLinkSelector = "[data-contact]";

    /// <summary>CSS selector for each dish row.</summary>
    public const string DishSelector = "[data-dish]";

    /// <summary>
    /// Builds a <see cref="ParsedContactLink"/> from a contact element's raw attributes. A blank/
    /// unknown kind defaults to <see cref="ParsedContactLinkKind.Website"/>.
    /// </summary>
    public static ParsedContactLink ToContactLink(string? rawKind, string url, string? label)
    {
        var kind = rawKind?.Trim().ToLowerInvariant() switch
        {
            "social" => ParsedContactLinkKind.Social,
            "phone" => ParsedContactLinkKind.Phone,
            _ => ParsedContactLinkKind.Website,
        };

        return new ParsedContactLink(kind, url.Trim(), string.IsNullOrWhiteSpace(label) ? null : label!.Trim());
    }

    /// <summary>
    /// Builds a raw <see cref="ParsedDish"/> from a dish row's extracted fields: the visible name, the
    /// raw price text (e.g. "120,00" / "₴ 120"), the optional weight label, the optional category hint,
    /// and the menu currency. Returns <c>null</c> when the name or price cannot be read — a malformed
    /// row is skipped rather than aborting the whole parse.
    /// </summary>
    public static ParsedDish? ToDish(string? name, string? rawPrice, string? weight, string? categoryHint, string currency)
    {
        if (string.IsNullOrWhiteSpace(name) || !TryParseAmount(rawPrice, out var amount))
        {
            return null;
        }

        var priceResult = Money.Create(amount, currency);
        if (priceResult.IsFailure)
        {
            return null;
        }

        return new ParsedDish(
            name.Trim(),
            priceResult.Value,
            string.IsNullOrWhiteSpace(weight) ? null : weight!.Trim(),
            string.IsNullOrWhiteSpace(categoryHint) ? null : categoryHint!.Trim());
    }

    // Parses a price string tolerantly: strips currency symbols/letters/spaces and accepts both '.'
    // and ',' as the decimal separator (Ukrainian menus commonly use a comma). Deterministic — no NLP.
    private static bool TryParseAmount(string? rawPrice, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(rawPrice))
        {
            return false;
        }

        var digits = new string(rawPrice.Where(c => char.IsDigit(c) || c is '.' or ',').ToArray())
            .Replace(',', '.');

        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
