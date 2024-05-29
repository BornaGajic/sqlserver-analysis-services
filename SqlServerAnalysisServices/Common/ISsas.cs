using Framework.Model;

namespace Framework.Common;

public interface ISsas : IDisposable
{
    void CancelProcessingAsync(string database);

    /// <summary>
    /// Changes default database.
    /// </summary>
    void ChangeDatabase(string database);

    /// <summary>
    /// Changes default effective user.
    /// </summary>
    void ChangeEffectiveUser(string effectiveUserName);

    IEnumerable<SsasDatabase> GetDatabases();

    int ProcessDatabase(string database, string table = null, string partition = null);

    IEnumerable<T> Query<T>(string query, object param = null, CancellationToken cancellationToken = default);

    IEnumerable<T> Query<T>(DaxQuery query, CancellationToken cancellationToken = default);
}