using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Extensions;
using Microsoft.PowerBI.Api;
using SqlServerAnalysisServices.Model;
using SqlServerAnalysisServices.Settings;

namespace SqlServerAnalysisServices.Service;

/// <summary>
/// A dataset's last-known refresh status, as reported by the Power BI Admin "Get Refreshables" API.
/// </summary>
public sealed record FabricRefreshStatus(string Name, string Status);

public class FabricSsas : Ssas
{
    private const string PowerBiApiBaseUrl = "https://api.powerbi.com";
    private readonly FabricCapacityManager _fabricManager;
    private readonly SsasConnection _ssasConnection;

    public FabricSsas(
        IOptions<SsasSettings> settings,
        SsasConnection ssasConnection,
        FabricCapacityManager fabricManager
    ) : base(settings, ssasConnection)
    {
        _fabricManager = fabricManager;
        _ssasConnection = ssasConnection;
    }

    // Power BI's "Cancel Refresh" REST API only cancels REST-triggered "enhanced refresh" jobs. Refreshes in
    // this codebase are issued via Process()/XMLA (see SemanticModelRefreshExecutor), which never creates one,
    // so there is no REST call that can cancel them. DISCOVER_SESSIONS/DISCOVER_LOCKS also don't reliably
    // expose a real SPID on Fabric, so falling through to base.CancelProcessing would call CancelSession with
    // a synthetic SPID against an unrelated session. There is currently no safe way to cancel a specific
    // Fabric-hosted refresh from here; a refresh triggered via Process() should be cancelled using that call's
    // own CancellationToken instead.
    public override void CancelProcessing(string databaseName, CancellationToken cancellation = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            base.CancelProcessing(databaseName, cancellation);
            return;
        }

        throw new NotSupportedException(
            $"Cancelling processing for '{databaseName}' is not supported against a Fabric/Power BI Premium XMLA endpoint. " +
            "Refreshes triggered via Process()/XMLA must be cancelled using that call's own CancellationToken."
        );
    }

    public override async ValueTask<SsasServer> GetServerDetailsAsync(CancellationToken cancellation = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return await base.GetServerDetailsAsync(cancellation);
        }

        if (!_fabricManager.IsConfigured)
        {
            return GetConnectedServerDetails();
        }

        var capacity = await _fabricManager.GetCapacityDataAsync(cancellation);

        return new SsasServer
        {
            Name = capacity.Name,
            FullName = capacity.Id?.ToString() ?? capacity.Name,
            Location = capacity.Location.ToString(),
            Tier = $"{capacity.Sku?.Tier} {capacity.Sku?.Name}".Trim(),
            State = capacity.Properties?.State?.ToString(),
            Created = capacity.SystemData?.CreatedOn?.UtcDateTime ?? DateTime.MinValue,
            CreatedBy = capacity.SystemData?.CreatedBy,
            LastModified = capacity.SystemData?.LastModifiedOn?.UtcDateTime ?? DateTime.MinValue,
            LastModifiedBy = capacity.SystemData?.LastModifiedBy,
            Administrators = capacity.Properties?.AdministrationMembers
        };
    }

    // $SYSTEM.DISCOVER_LOCKS does not report meaningful data against a Fabric/Power BI Premium XMLA endpoint, so
    // processing state is derived from the Power BI Admin "Get Refreshables" API instead.
    public override IEnumerable<SsasLock> GetSsasLocks(string databaseName = null, CancellationToken cancellation = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return base.GetSsasLocks(databaseName, cancellation);
        }

        var statuses = GetRefreshStatuses(cancellation);

        return statuses
            .Where(status => string.IsNullOrWhiteSpace(databaseName) || string.Equals(status.Name, databaseName, StringComparison.OrdinalIgnoreCase))
            .Where(IsRefreshInProgress)
            .Select(status => new SsasLock
            {
                LOCK_TYPE = SsasLockType.LOCK_WRITE,
                Session = new SsasSession { SESSION_CURRENT_DATABASE = status.Name }
            })
            .ToList();
    }

    public override bool PauseServer(CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return base.PauseServer(cancellationToken);
        }

        return PauseServerAsync(cancellationToken).GetAwaiter().GetResult();
    }

    public override async Task<bool> PauseServerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return await base.PauseServerAsync(cancellationToken);
        }

        if (!_fabricManager.IsConfigured)
        {
            return false;
        }

        if (await _fabricManager.IsActiveAsync() && IsProcessing(cancellation: cancellationToken))
        {
            throw new Exception("A database is currently being processed.");
        }

        return await _fabricManager.SuspendAsync(cancellationToken: cancellationToken);
    }

    public override async Task<bool> ScaleAsync(string skuTier, bool withShutdown, CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return await base.ScaleAsync(skuTier, withShutdown, cancellationToken);
        }

        if (!_fabricManager.IsConfigured)
        {
            return false;
        }

        if (await _fabricManager.IsActiveAsync() && IsProcessing(cancellation: cancellationToken))
        {
            throw new Exception("A database is currently being processed.");
        }

        return await _fabricManager.ScaleAsync(skuTier, withShutdown, cancellationToken: cancellationToken);
    }

    public override bool StartServer(CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return base.StartServer(cancellationToken);
        }

        return StartServerAsync(cancellationToken).GetAwaiter().GetResult();
    }

    public override async Task<bool> StartServerAsync(CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return await base.StartServerAsync(cancellationToken);
        }

        return _fabricManager.IsConfigured
            && await _fabricManager.ResumeAsync(cancellationToken: cancellationToken);
    }

    protected internal override AdomdConnection GetConnection()
    {
        EnsureCapacityAvailable();

        return base.GetConnection();
    }

    /// <summary>
    /// Queries the Power BI Admin "Get Refreshables" API for each dataset's last refresh status.
    /// </summary>
    protected internal IReadOnlyList<FabricRefreshStatus> GetRefreshStatuses(CancellationToken cancellation = default)
    {
        var token = _ssasConnection.GetAzureSsasTokenCredential().GetPowerBiToken(cancellation);
        var client = new PowerBIClient(token.Token, new Uri(PowerBiApiBaseUrl));

        var response = client.Admin.GetRefreshables(
            top: 100,
            filter: "lastRefresh ne null",
            cancellationToken: cancellation
        );

        return response.Value.Value
            .Select(refreshable => new FabricRefreshStatus(refreshable.Name, refreshable.LastRefresh?.Status))
            .ToList();
    }

    protected internal override Server GetServer(bool propertiesOnly = false)
    {
        EnsureCapacityAvailable();

        return base.GetServer(propertiesOnly);
    }

    private static bool IsRefreshInProgress(FabricRefreshStatus status) =>
        status.Status?.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == true ||
        status.Status?.Contains("progress", StringComparison.OrdinalIgnoreCase) == true;

    private void EnsureCapacityAvailable()
    {
        if (!_fabricManager.IsConfigured || _fabricManager.IsActive())
        {
            return;
        }

        _fabricManager.Resume();
    }

    private SsasServer GetConnectedServerDetails()
    {
        using var server = GetServer(propertiesOnly: true);

        return new SsasServer
        {
            Name = server.Name,
            FullName = server.Name,
            Created = server.CreatedTimestamp,
            State = server.Connected ? "Active" : "Unknown",
            CreatedBy = server.Name,
            Location = server.ServerLocation.ToString(),
            LastModified = server.LastSchemaUpdate
        };
    }

    private bool IsFabricPowerBIEndpoint() => _ssasConnection.IsFabricPowerBIEndpoint();
}