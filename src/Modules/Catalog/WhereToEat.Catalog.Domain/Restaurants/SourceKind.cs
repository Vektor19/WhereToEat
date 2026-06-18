namespace WhereToEat.Catalog.Domain.Restaurants;

/// <summary>
/// Where a piece of catalog data came from. The parser is a "rough fill" and the admin is the
/// source of truth (invariant #3): an <see cref="Admin"/>-sourced <see cref="MenuItem"/> is
/// curated data the parser must not overwrite, whereas <see cref="Parsed"/> data is the
/// auto-filled draft. Carried on each <see cref="MenuItem"/> so the persist gate (later steps)
/// can reason about provenance alongside the per-item <c>DoNotParse</c> flag.
/// </summary>
public enum SourceKind
{
    /// <summary>Auto-filled by the parser from a first-party site (the draft, overwritable).</summary>
    Parsed = 0,

    /// <summary>Entered or corrected by an admin (the source of truth, protected).</summary>
    Admin = 1,
}
