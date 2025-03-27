using Dapper;
using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Model;
using Microsoft.AnalysisServices.AdomdClient;
using TOM = Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.DependencyInjection;
using SqlServerAnalysisServices.Extensions;
using Azure.ResourceManager.Analysis;
using Azure.ResourceManager;
using Azure.ResourceManager.Analysis.Models;

namespace SqlServerAnalysisServices.Service
{
    public class Ssas : ISsas
    {
        protected bool _disposed;
        private readonly AdomdConnection _rootConnection;

        [ActivatorUtilitiesConstructor]
        public Ssas(AdomdConnection connection)
        {
            _rootConnection = connection;

            AzureServerResource = new Lazy<AnalysisServerResource>(() =>
            {
                using var connection = GetConnection();

                if (connection.IsCloudAnalysisServices())
                {
                    var ssasConnection = (SsasConnection)connection.AccessToken.UserContext;
                    var tokenCredential = ssasConnection.GetAzureSsasTokenCredential();
                    var serverName = ssasConnection.DataSource[(ssasConnection.DataSource.LastIndexOf('/') + 1)..];

                    var ssasResource = AnalysisServerResource.CreateResourceIdentifier(
                        ssasConnection.AzureResource.SubscriptionId, ssasConnection.AzureResource.ResourceGroupName, serverName
                    );
                    var armClient = new ArmClient(tokenCredential);

                    return armClient.GetAnalysisServerResource(ssasResource);
                }

                return null;
            });
        }

        public event EventHandler Disposed;

        private Lazy<AnalysisServerResource> AzureServerResource { get; }

        public virtual void CancelProcessing(string databaseName, CancellationToken cancellation = default)
        {
            var dbLockSpids =
                from @lock in GetSsasLocks(databaseName, cancellation)
                where @lock.LOCK_TYPE is SsasLockType.LOCK_WRITE or SsasLockType.LOCK_READ
                select @lock.SPID;

            cancellation.ThrowIfCancellationRequested();

            using var server = GetServer();

            foreach (var sessionId in dbLockSpids)
            {
                server.CancelSession(sessionId, true);
            }
        }

        public virtual void ChangeEffectiveUser(string effectiveUserName)
            => _rootConnection.ChangeEffectiveUser(effectiveUserName);

        /// <inheritdoc/>
        public ISsasDatabaseStructure DatabaseStructure(string databaseName = null)
            => new SsasDatabaseStructure(string.IsNullOrEmpty(databaseName) ? _rootConnection.Database : databaseName, this);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public virtual IEnumerable<SsasDatabase> GetDatabases(CancellationToken cancellation = default)
        {
            using var server = GetServer();

            foreach (TOM.Database database in server.Databases)
            {
                cancellation.ThrowIfCancellationRequested();

                yield return DatabaseStructure(database.Name).Properties();
            }
        }

        public async ValueTask<SsasServer> GetServerDetailsAsync(CancellationToken cancellation = default)
        {
            using var connection = GetConnection();

            if (connection.IsCloudAnalysisServices())
            {
                var ssasServerResource = AzureServerResource.Value;
                var serverDataResponse = await ssasServerResource.GetAsync(cancellation);
                var serverData = serverDataResponse.Value.Data;

                return new SsasServer
                {
                    Name = serverData.Name,
                    Location = serverData.Location,
                    Tier = $"{serverData.AnalysisSku.Tier} {serverData.AnalysisSku.Name} ({serverData.AnalysisSku.Capacity} instance)".Trim(),
                    State = serverData.State?.ToString(),
                    Created = serverData.SystemData?.CreatedOn?.UtcDateTime ?? DateTime.MinValue,
                    CreatedBy = serverData.SystemData?.CreatedBy,
                    LastModified = serverData.SystemData?.LastModifiedOn?.UtcDateTime ?? DateTime.MinValue,
                    LastModifiedBy = serverData.SystemData?.LastModifiedBy,
                    Administrators = serverData.AsAdministratorIdentities
                };
            }

            using var server = GetServer();

            return new SsasServer
            {
                Name = server.Name,
                Created = server.CreatedTimestamp,
                State = "Succeeded",
                CreatedBy = server.Name,
                Location = server.ServerLocation.ToString(),
                LastModified = server.LastSchemaUpdate
            };
        }

        public virtual IEnumerable<SsasLock> GetSsasLocks(string databaseName = null, CancellationToken cancellation = default)
        {
            using var connection = GetConnection();
            connection.Open();

            return from ssasLock in connection.Query<SsasLock>(new CommandDefinition("SELECT * FROM [$SYSTEM].[DISCOVER_LOCKS]", cancellationToken: cancellation))
                   join ssasSession in connection.Query<SsasSession>(new CommandDefinition("SELECT * FROM [$SYSTEM].[DISCOVER_SESSIONS]", cancellationToken: cancellation))
                       on ssasLock.SPID equals ssasSession.SESSION_SPID
                   where
                       string.IsNullOrWhiteSpace(databaseName) || ssasSession.SESSION_CURRENT_DATABASE == databaseName
                   select ssasLock with
                   {
                       Session = ssasSession
                   };
        }

        /// <inheritdoc/>
        public bool IsProcessing(string databaseName = null, CancellationToken cancellation = default)
            => GetSsasLocks(databaseName, cancellation).Any(@lock => @lock.LOCK_TYPE is SsasLockType.LOCK_WRITE or SsasLockType.LOCK_READ);

        public ISsasRoleManager ManageDatabaseRoles(string databaseName) => new SsasRoleManager(databaseName, this);

        public bool PauseServer(CancellationToken cancellationToken = default)
        {
            if (IsProcessing(cancellation: cancellationToken))
            {
                throw new Exception("A database is currently being processed.");
            }

            var result = AzureServerResource.Value?.Suspend(Azure.WaitUntil.Completed, cancellationToken);
            return result?.HasCompleted ?? false;
        }

        public async Task<bool> PauseServerAsync(CancellationToken cancellationToken = default)
        {
            if (IsProcessing(cancellation: cancellationToken))
            {
                throw new Exception("A database is currently being processed.");
            }

            var result = await AzureServerResource.Value?.SuspendAsync(Azure.WaitUntil.Completed, cancellationToken);
            return result?.HasCompleted ?? false;
        }

        public virtual int Process(Action<SsasProcessScriptBuilder> configurator, CancellationToken cancellation = default)
        {
            ArgumentNullException.ThrowIfNull(configurator);

            var scriptBuilder = new SsasProcessScriptBuilder();
            configurator(scriptBuilder);

            return Process(scriptBuilder.Build(), cancellation);
        }

        // <inheritdoc/>
        public virtual int Process(string script, CancellationToken cancellation = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(script);

            using var connection = GetConnection();
            using var cmd = connection.CreateCommand();
            connection.Open();
            cmd.CommandText = script;
            using var cancellationRegistration = cancellation.Register(cmd.Cancel);

            return cmd.ExecuteNonQuery();
        }

        public virtual IEnumerable<T> Query<T>(DaxQuery query, CancellationToken cancellationToken = default)
            => GetConnection().ExecuteQuery<T>(query, cancellationToken);

        public virtual IEnumerable<T> Query<T>(string query, object param = null, CancellationToken cancellationToken = default) =>
            Query<T>(
                new DaxQuery
                {
                    Query = query,
                    Param = param
                },
                cancellationToken
            );

        public async Task<bool> ScaleAsync(string skuTier, CancellationToken cancellationToken = default)
        {
            if (IsProcessing(cancellation: cancellationToken))
            {
                throw new Exception("A database is currently being processed.");
            }

            var result = await AzureServerResource.Value?.UpdateAsync(Azure.WaitUntil.Completed, new AnalysisServerPatch
            {
                Sku = new AnalysisResourceSku(skuTier)
            }, cancellationToken);

            return result?.HasCompleted ?? false;
        }

        public async ValueTask<string> SendXmlaRequestAsync(XmlaSoapRequest request, CancellationToken cancellationToken = default)
        {
            using var connection = GetConnection();
            using var server = GetServer();

            if (!string.IsNullOrWhiteSpace(request.Settings?.EffectiveUserName))
            {
                request = request with
                {
                    Request = XmlaParsing.ApplyEffectiveUserName(request.Request, request.Settings.EffectiveUserName)
                };
            }

            if (connection.IsCloudAnalysisServices())
            {
                return await server.SendAzureXmlaRequestAsync(request, cancellationToken);
            }

            return server.SendLocalhostXmlaRequest(request, cancellationToken);
        }

        public bool StartServer(CancellationToken cancellationToken = default)
        {
            var result = AzureServerResource.Value?.Resume(Azure.WaitUntil.Completed, cancellationToken);
            return result?.HasCompleted ?? false;
        }

        public async Task<bool> StartServerAsync(CancellationToken cancellationToken = default)
        {
            var result = await AzureServerResource.Value?.ResumeAsync(Azure.WaitUntil.Completed, cancellationToken);
            return result?.HasCompleted ?? false;
        }

        internal AdomdConnection GetConnection()
        {
            if (_disposed)
            {
                throw new Exception("Instance is disposed.");
            }

            // Likely will not ever happen. Internally, AdomdConnection is executing the OnAccessTokenExpired just before the token expires.
            // This is here in case the internal process fails.
            if (_rootConnection.IsCloudAnalysisServices() && _rootConnection.AccessToken.IsExpired)
            {
                lock (_rootConnection)
                {
                    if (_rootConnection.AccessToken.IsExpired)
                    {
                        var ssasConnection = (SsasConnection)_rootConnection.AccessToken.UserContext;
                        _rootConnection.AccessToken = ssasConnection.GetAzureSsasAccessToken();
                    }
                }
            }

            var clone = _rootConnection.Clone();

            if (clone.IsCloudAnalysisServices())
            {
                clone.AccessToken = _rootConnection.AccessToken;
                clone.OnAccessTokenExpired = _rootConnection.OnAccessTokenExpired;
            }

            return clone;
        }

        /// <summary>
        /// <paramref name="propertiesOnly"/> if set to true the server will only load server properties.
        /// </summary>
        internal TOM.Server GetServer(bool propertiesOnly = false)
        {
            using var connection = GetConnection();
            var server = new TOM.Server();

            if (connection.IsCloudAnalysisServices())
            {
                server.AccessToken = connection.AccessToken;
                server.OnAccessTokenExpired = connection.OnAccessTokenExpired;
            }

            try
            {
                server.Connect(connection.ConnectionString, propertiesOnly);
                return server;
            }
            catch
            {
                return server;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _rootConnection.Dispose();
                }

                _disposed = true;
                Disposed?.Invoke(this, new());
            }
        }
    }
}