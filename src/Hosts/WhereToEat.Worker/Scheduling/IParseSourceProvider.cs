using WhereToEat.Parsing.Domain;

namespace WhereToEat.Worker.Scheduling;

/// <summary>
/// Supplies the first-party source descriptors the weekly parse job iterates. A host-level seam (the
/// worker's own composition concern) so the set of sources is configuration-driven and the
/// <see cref="WhereToEat.Worker.Jobs.WeeklyParseJob"/> stays a thin "for each source, delegate to the
/// Step 9 pipeline" shell. The descriptors carry only the first-party URL + strategy key + name; the
/// compliance gates (first-party allow-list / robots.txt / rate-limit — invariant #9) still run inside
/// the Step 9 handler before any fetch, so an unexpected source is rejected there, not here.
/// </summary>
public interface IParseSourceProvider
{
    /// <summary>Returns the first-party sources to parse on the weekly schedule (possibly empty).</summary>
    IReadOnlyList<SourceDescriptor> GetSources();
}
