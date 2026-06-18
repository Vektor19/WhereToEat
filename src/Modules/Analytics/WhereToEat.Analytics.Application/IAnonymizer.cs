using WhereToEat.Analytics.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Analytics.Application;

/// <summary>
/// Applies the <b>retain / hash / drop</b> anonymization rules to a <see cref="RawAnalyticsEvent"/>
/// <b>at ingest, before persistence</b> (§8.1 / invariant #11), producing the persistable
/// <see cref="AnalyticsEvent"/> domain aggregate:
/// <list type="bullet">
///   <item><b>retain</b> the non-identifying dimensions (dish/category ids, sort mode, filters, result
///   position);</item>
///   <item><b>coarsen</b> the time to its hour bucket and any precise lat/lng to a neighbourhood-grade
///   geohash;</item>
///   <item><b>hash</b> any user/session id with a rotating-salted, non-reversible digest;</item>
///   <item><b>drop</b> the precise lat/lng and the raw id — they are never carried into the result.</item>
/// </list>
/// Because the result is the anonymized domain aggregate (which cannot represent a raw id / precise
/// coordinate), no raw PII can flow downstream to the writer.
/// </summary>
public interface IAnonymizer
{
    /// <summary>
    /// Anonymizes <paramref name="raw"/> into a persistable <see cref="AnalyticsEvent"/>. Returns a
    /// validation failure if the raw payload cannot yield a valid anonymized event (e.g. an out-of-range
    /// coordinate); the raw fields are never returned and never persisted.
    /// </summary>
    Result<AnalyticsEvent> Anonymize(RawAnalyticsEvent raw);
}
