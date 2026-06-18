using System.Data;

namespace WhereToEat.BuildingBlocks.Persistence;

/// <summary>
/// Opens SQL Server connections for the Dapper-backed hot/read paths. A port so call sites
/// (repositories, candidate sources) depend on this abstraction rather than on
/// <c>Microsoft.Data.SqlClient.SqlConnection</c> directly — the provider stays swappable and the
/// connection string lives in one place (the DI registration).
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Creates and <b>opens</b> a new connection. The caller owns it and must dispose it
    /// (Dapper's <c>using</c> pattern). A fresh connection per unit of work keeps the pool the
    /// single source of connection reuse, which is the recommended ADO.NET model.
    /// </summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
