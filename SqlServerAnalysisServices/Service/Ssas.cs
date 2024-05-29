using Dapper;
using FastMember;
using Framework.Attribute;
using Framework.Common;
using Framework.Model;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.DependencyInjection;

namespace Framework.Service
{
    // DMV query: https://www.sqlservercentral.com/articles/monitor-current-analysis-services-activity
    public class Ssas : ISsas
    {
        protected readonly AdomdConnection _connection;
        protected readonly Server _server;
        protected bool _disposed;

        [ActivatorUtilitiesConstructor]
        public Ssas(Server server, AdomdConnection connection)
        {
            _server = server;
            _connection = connection;
        }

        public event EventHandler Disposed;

        public virtual void CancelProcessingAsync(string database)
        {
            var dbLockSpids =
                from @lock in GetSsasLocks(database)
                where
                    (@lock.LOCK_TYPE is SsasLockType.LOCK_WRITE or SsasLockType.LOCK_READ)
                select @lock.SPID;

            foreach (var sessionId in dbLockSpids)
            {
                _server.CancelSession(sessionId, true);
            }
        }

        public virtual void ChangeDatabase(string database) => _connection.ChangeDatabase(database);

        public virtual void ChangeEffectiveUser(string effectiveUserName) => _connection.ChangeEffectiveUser(effectiveUserName);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public virtual IEnumerable<SsasDatabase> GetDatabases()
        {
            _connection.RefreshMetadata();

            var currentLocks = GetSsasLocks().ToList();

            foreach (Database database in _server.Databases)
            {
                database.Refresh();
                var dbLocks = currentLocks.Where(@lock => @lock.Session.SESSION_CURRENT_DATABASE == database.Name);

                yield return new SsasDatabase
                {
                    DatabaseId = database.ID,
                    DatabaseName = database.Name,
                    Model = database.Model.Name,
                    Description = database.Description,
                    Size = database.EstimatedSize,
                    LastProcessedUtc = database.LastProcessed.ToUniversalTime(),
                    LastUpdatedUtc = database.LastUpdate.ToUniversalTime(),
                    IsProcessing = dbLocks.Any(l => l.LOCK_TYPE is SsasLockType.LOCK_WRITE or SsasLockType.LOCK_READ)
                };
            }
        }

        public virtual IEnumerable<SsasLock> GetSsasLocks(string database = null) =>
            from ssasLock in _connection.Query<SsasLock>(new CommandDefinition("SELECT * FROM [$SYSTEM].[DISCOVER_LOCKS]"))
            join ssasSession in _connection.Query<SsasSession>(new CommandDefinition("SELECT * FROM [$SYSTEM].[DISCOVER_SESSIONS]"))
                on ssasLock.SPID equals ssasSession.SESSION_SPID
            where
                string.IsNullOrWhiteSpace(database) || ssasSession.SESSION_CURRENT_DATABASE == database
            select ssasLock with
            {
                Session = ssasSession
            };

        public virtual int ProcessDatabase(string database, string table = null, string partition = null)
        {
            using var connection = _connection.Clone();
            using var cmd = connection.CreateCommand();

            connection.Open();

            cmd.CommandText = $$"""
            {
                "refresh": {
                    "type": "full",
                    "objects": [
                        {
                            "database": "{{database}}",
                            "table": "{{table ?? string.Empty}}",
                            "partition": {{partition ?? string.Empty}}
                        }
                    ]
                }
            }
            """;

            return cmd.ExecuteNonQuery();
        }

        public virtual IEnumerable<T> Query<T>(DaxQuery query, CancellationToken cancellationToken = default)
        {
            using var connection = _connection.Clone();
            connection.Open();

            if (!string.IsNullOrWhiteSpace(query.Settings?.EffectiveUserName)) connection.ChangeEffectiveUser(query.Settings.EffectiveUserName);
            if (!string.IsNullOrWhiteSpace(query.Settings?.Database)) connection.ChangeDatabase(query.Settings.Database);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = query.Query;

            if (query.Param is not null)
            {
                var typeAccessor = TypeAccessor.Create(query.Param.GetType());

                foreach (var member in typeAccessor.GetMembers())
                {
                    var excludeParam =
                        member.GetAttribute(typeof(SkipSsasQueryParameter), false) is SkipSsasQueryParameter skipQueryAttribute
                        && (
                            skipQueryAttribute.Condition.HasFlag(SkipSsasQueryParameter.SkipCondition.Skip)
                            || (
                                skipQueryAttribute.Condition.HasFlag(SkipSsasQueryParameter.SkipCondition.SkipIfNull)
                                && typeAccessor[query.Param, member.Name] is null
                            )
                        );

                    if (!excludeParam)
                        cmd.Parameters.Add(member.Name, typeAccessor[query.Param, member.Name]);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var cancellationRegistration = cancellationToken.Register(cmd.Cancel);

            var resultTypeAccessor = TypeAccessor.Create(typeof(T));
            using var adomdDataReader = cmd.ExecuteReader();

            foreach (var row in adomdDataReader)
            {
                var resultItem = resultTypeAccessor.CreateNew();

                foreach (var resulTypeMember in resultTypeAccessor.GetMembers())
                {
                    resultTypeAccessor[resultItem, resulTypeMember.Name] = ChangeType(row[$"[{resulTypeMember.Name}]"], resulTypeMember.Type);
                }

                yield return (T)resultItem;
            }
        }

        public virtual IEnumerable<T> Query<T>(string query, object param = null, CancellationToken cancellationToken = default) => Query<T>(new DaxQuery
        {
            Query = query,
            Param = param
        }, cancellationToken);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _server.Dispose();
                    _connection.Dispose();
                }

                _disposed = true;
                Disposed?.Invoke(this, new());
            }
        }

        private static object ChangeType(object value, Type conversion)
        {
            var t = conversion;

            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (value == null)
                {
                    return null;
                }

                t = Nullable.GetUnderlyingType(t);
            }

            return Convert.ChangeType(value, t);
        }
    }
}