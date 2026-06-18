using WhereToEat.Parsing.Domain;

namespace WhereToEat.Parsing.Application;

/// <summary>
/// The command that runs the full parse pipeline for a single first-party source: compliance →
/// fetch → normalize (mapped persist + unmapped → quarantine) → geocode → admin-protected persist.
/// Carries the <see cref="Source"/> descriptor (URL, strategy key, restaurant name); the worker
/// (Step 12) issues one per first-party source on the weekly schedule.
/// </summary>
public sealed record RunParseCommand(SourceDescriptor Source);
