using Framework.Model;

namespace Framework.Common;

public interface ISsas : IDisposable
{
    void CancelProcessing(string databaseName, CancellationToken cancellation = default);

    /// <summary>
    /// Returns additional database information.
    /// </summary>
    SsasDatabaseDescription Describe(string databaseName, CancellationToken cancellationToken = default);

    IEnumerable<SsasDatabase> GetDatabases(CancellationToken cancellationToken = default);

    ValueTask<SsasServer> GetServerDetailsAsync(CancellationToken cancellationToken = default);

    IEnumerable<SsasLock> GetSsasLocks(string databaseName = null, CancellationToken cancellation = default);

    /// <summary>
    /// Checks whether a specific database is being processed or if any are processing
    /// </summary>
    bool IsProcessing(string databaseName, CancellationToken cancellation = default);

    ISsasRoleManager ManageDatabaseRoles(string databaseName);

    bool PauseServer(CancellationToken cancellationToken = default);

    Task<bool> PauseServerAsync(CancellationToken cancellationToken = default);

    int Process(string script, CancellationToken cancellation = default);

    IEnumerable<T> Query<T>(string query, object param = null, CancellationToken cancellationToken = default);

    IEnumerable<T> Query<T>(DaxQuery query, CancellationToken cancellationToken = default);

    ValueTask<string> SendXmlaRequestAsync(XmlaSoapRequest request, CancellationToken cancellationToken = default);

    bool StartServer(CancellationToken cancellationToken = default);

    Task<bool> StartServerAsync(CancellationToken cancellationToken = default);
}