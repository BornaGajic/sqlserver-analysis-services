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
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Settings;

namespace SqlServerAnalysisServices.Service;

public class Ssas : ISsas
{
    private readonly SsasConnection _ssasConnection;

    [ActivatorUtilitiesConstructor]
    public Ssas(IOptions<SsasSettings> settings, SsasConnection ssasConnection)
    {
        _ssasConnection = ssasConnection;

        AzureServerResource = new Lazy<AnalysisServerResource>(() =>
        {
            using var connection = GetConnection();

            if (connection.IsCloudAnalysisServices())
            {
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

    /// <inheritdoc/>
    public ISsasDatabaseStructure DatabaseStructure(string databaseName = null)
    {
        var dbName = databaseName;

        if (string.IsNullOrEmpty(dbName))
        {
            using var connection = GetConnection();
            dbName = connection.Database;
        }

        return new SsasDatabaseStructure(dbName, this);
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
                FullName = serverData.ServerFullName,
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
            FullName = server.Name,
            Created = server.CreatedTimestamp,
            State = server.Connected ? "Succeeded" : "Suspended",
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
    {
        using var connection = GetConnection();
        return connection.ExecuteQuery<T>(query, cancellationToken).ToList();
    }

    public virtual IEnumerable<T> Query<T>(string query, object param = null, CancellationToken cancellationToken = default) =>
        Query<T>(
            new DaxQuery
            {
                Query = query,
                Param = param
            },
            cancellationToken
        );

    public virtual T Scalar<T>(DaxQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = GetConnection();
        return connection.ExecuteScalar<T>(query, cancellationToken);
    }

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
        if (AzureServerResource.Value is not null)
        {
            var result = await AzureServerResource.Value?.ResumeAsync(Azure.WaitUntil.Completed, cancellationToken);
            return result.HasCompleted;
        }

        return false;
    }

    internal AdomdConnection GetConnection()
    {
        var connection = new AdomdConnection(_ssasConnection.ConnectionString);

        if (connection.IsCloudAnalysisServices())
        {
            connection.AccessToken = _ssasConnection.GetAzureSsasAccessToken();
            connection.OnAccessTokenExpired = oldToken => _ssasConnection.GetAzureSsasAccessToken();
        }

        return connection;
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
}