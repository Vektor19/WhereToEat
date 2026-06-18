namespace WhereToEat.BuildingBlocks.Caching;

/// <summary>
/// Binds the <c>Redis</c> configuration section: the connection string the multiplexer connects to and
/// an <see cref="Enabled"/> switch so a host can run without Redis (then the no-op cache is used and the
/// hot paths fall through to the DB-only providers — Step 7/12 stay correct with caching off).
/// </summary>
public sealed class RedisCacheOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// When <c>false</c> (or no connection string is configured), <c>AddRedisCache</c> registers the
    /// <see cref="NullCacheService"/> instead of connecting — the read-through caches degrade to DB-only.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>The StackExchange.Redis connection string (e.g. <c>redis:6379</c>).</summary>
    public string ConnectionString { get; set; } = string.Empty;
}
