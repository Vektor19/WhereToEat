using WhereToEat.Parsing.Domain;

namespace WhereToEat.Parsing.Application;

/// <summary>
/// The persistence port for the quarantine / review queue (invariant #2 — unmappable dishes are
/// quarantined, never dropped). The normalizer routes raw dish lines it could not map to a canonical
/// dish here, for later admin resolution; a quarantined item is never written as a live menu item
/// until an admin maps it. The Dapper-backed implementation appends to the
/// <c>parsing.ParseQuarantine</c> table.
/// </summary>
public interface IParseQuarantineStore
{
    /// <summary>Appends the unmappable <paramref name="items"/> to the quarantine queue.</summary>
    Task AddAsync(IReadOnlyList<ParseQuarantineItem> items, CancellationToken cancellationToken = default);
}
