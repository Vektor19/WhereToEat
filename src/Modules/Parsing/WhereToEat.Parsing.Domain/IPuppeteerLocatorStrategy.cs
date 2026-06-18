namespace WhereToEat.Parsing.Domain;

/// <summary>
/// <b>Declared-only seam — NOT implemented (a non-goal, design §"Non-Goals").</b> The future Puppeteer
/// locator strategy would <i>discover</i> restaurant source pages (production-grade scraping of a
/// venue's site to locate its menu), feeding <see cref="SourceDescriptor"/>s into the same parser
/// pipeline. It is declared now purely so the abstraction has a place for it and a later step can plug
/// it in <b>without</b> touching the engine (invariant #4). There is deliberately no concrete type
/// implementing this interface anywhere in the solution.
/// </summary>
public interface IPuppeteerLocatorStrategy
{
    /// <summary>
    /// Would discover the source descriptors for a venue's first-party site. Unimplemented: the
    /// Puppeteer locator is out of scope and exists only as this seam.
    /// </summary>
    Task<IReadOnlyList<SourceDescriptor>> LocateSourcesAsync(Uri venueSite, CancellationToken cancellationToken = default);
}
