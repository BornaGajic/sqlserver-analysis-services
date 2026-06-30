using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Model;
using SqlServerAnalysisServices.Settings;

namespace SqlServerAnalysisServices.Service;

public class FabricSsas : Ssas
{
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

    public override async Task<bool> ScaleAsync(string skuTier, CancellationToken cancellationToken = default)
    {
        if (!IsFabricPowerBIEndpoint())
        {
            return await base.ScaleAsync(skuTier, cancellationToken);
        }

        if (!_fabricManager.IsConfigured)
        {
            return false;
        }

        if (await _fabricManager.IsActiveAsync() && IsProcessing(cancellation: cancellationToken))
        {
            throw new Exception("A database is currently being processed.");
        }

        return await _fabricManager.ScaleAsync(skuTier, cancellationToken: cancellationToken);
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

    protected internal override Server GetServer(bool propertiesOnly = false)
    {
        EnsureCapacityAvailable();

        return base.GetServer(propertiesOnly);
    }

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