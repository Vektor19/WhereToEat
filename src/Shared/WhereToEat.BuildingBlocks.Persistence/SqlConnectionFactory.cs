using System.Data;
using Microsoft.Data.SqlClient;

namespace WhereToEat.BuildingBlocks.Persistence;

/// <summary>
/// The default <see cref="ISqlConnectionFactory"/> over <see cref="SqlConnection"/>: every call
/// hands back a freshly-opened connection from the ADO.NET pool. The connection string is supplied
/// once at construction (from configuration in the host's composition root), so no SQL credentials
/// leak into the call sites.
/// </summary>
public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>Creates the factory with the SQL Server connection string, rejecting a blank one.</summary>
    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            // Don't leak a half-open connection if OpenAsync throws (e.g. transient network).
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
