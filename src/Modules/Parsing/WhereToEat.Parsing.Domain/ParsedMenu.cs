namespace WhereToEat.Parsing.Domain;

/// <summary>
/// The <b>normalized menu contract</b> every parser strategy produces identically, regardless of how
/// it fetched the page (Selenium for JS-rendered sites, AngleSharp for static HTML, a future Puppeteer
/// locator): the restaurant <see cref="ParsedRestaurantFacts"/> header plus the raw
/// <see cref="ParsedDish"/> lines. It is deliberately <b>pre-normalization</b> — the raw dish names
/// are mapped onto the canonical taxonomy by the <see cref="INormalizer"/> downstream — so the
/// strategy's only job is "fetch the page, read the facts", and the e2e/integration tests assert this
/// one shape for both strategy kinds.
/// </summary>
public sealed record ParsedMenu(ParsedRestaurantFacts Restaurant, IReadOnlyList<ParsedDish> Dishes);
