using Azure.ResourceManager;
using Azure;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Model;
using SqlServerAnalysisServices.Settings;
using System.Runtime.Caching;
using Azure.ResourceManager.Fabric;
using Azure.ResourceManager.Fabric.Models;
using Nito.AsyncEx;
using SqlServerAnalysisServices.Extensions;

namespace SqlServerAnalysisServices.Service;

public class FabricCapacityManager
{
    internal const string NotConfiguredMessage = "Fabric capacity management is not configured.";
    private const string CapacityDataCacheKey = "capacity-data";
    private static readonly MemoryCache _memoryCache = new MemoryCache(nameof(FabricCapacityManager));
    private readonly AzureResource _azureResource;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public FabricCapacityManager(IOptions<SsasSettings> ssasSettings)
    {
        _azureResource = ssasSettings.Value.Azure;
        FabricResource = new Lazy<FabricCapacityResource>(() =>
        {
            var azTokenService = new AzureTokenCredentialService();
            var armClient = new ArmClient(azTokenService.GetAzureTokenCredential(_azureResource));

            var resourceId = FabricCapacityResource.CreateResourceIdentifier(
                _azureResource.SubscriptionId,
                _azureResource.ResourceGroupName,
                _azureResource.ResourceName
            );

            return armClient.GetFabricCapacityResource(resourceId);
        });
    }

    public virtual bool IsConfigured => IsConfiguredAzureResource(_azureResource);
    private Lazy<FabricCapacityResource> FabricResource { get; }

    public virtual FabricCapacityData GetCapacityData(CancellationToken cancellation = default)
    {
        EnsureConfigured();

        return _memoryCache.GetOrAdd(
            CapacityDataCacheKey,
            GetServerDetailsCore,
            absoluteExpirationFromNow: TimeSpan.FromMinutes(10)
        );

        FabricCapacityData GetServerDetailsCore()
        {
            var response = FabricResource.Value.Get(cancellation);
            return response.Value.Data;
        }
    }

    public virtual async ValueTask<FabricCapacityData> GetCapacityDataAsync(CancellationToken cancellation = default)
    {
        EnsureConfigured();

        return await _memoryCache.GetOrAddAsync(
            CapacityDataCacheKey,
            GetServerDetailsCore,
            absoluteExpirationFromNow: TimeSpan.FromMinutes(10)
        );

        async ValueTask<FabricCapacityData> GetServerDetailsCore()
        {
            var response = await FabricResource.Value.GetAsync(cancellation);
            return response.Value.Data;
        }
    }

    public virtual bool IsActive() => IsConfigured && IsCapacityInState(FabricResourceState.Active);

    public virtual async ValueTask<bool> IsActiveAsync() => IsConfigured && await IsCapacityInStateAsync(FabricResourceState.Active);

    public virtual bool Resume(
        Action onStartHandler = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureConfigured();

        return StartOperation(
            ct => IsCapacityInState(FabricResourceState.Active, ct),
            FabricResource.Value.Resume,
            onStartHandler,
            cancellationToken
        );
    }

    public virtual async Task<bool> ResumeAsync(
        Func<Task> onStartHandler = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        return await StartOperationAsync(
            ct => IsCapacityInStateAsync(FabricResourceState.Active, ct),
            FabricResource.Value.ResumeAsync,
            onStartHandler,
            cancellationToken
        );
    }

    public virtual bool Scale(
        string tier,
        Action onStartHandler = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(tier))
        {
            throw new ArgumentException("A target tier (SKU) must be provided to scale the capacity.", nameof(tier));
        }

        var patch = new FabricCapacityPatch
        {
            Sku = new FabricSku(tier, FabricSkuTier.Fabric)
        };

        return StartOperation(
            ct => IsCapacityAtTier(tier, ct),
            (waitUntil, ct) => FabricResource.Value.Update(waitUntil, patch, ct),
            onStartHandler,
            cancellationToken
        );
    }

    public virtual async Task<bool> ScaleAsync(
        string tier,
        Func<Task> onStartHandler = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(tier))
        {
            throw new ArgumentException("A target tier (SKU) must be provided to scale the capacity.", nameof(tier));
        }

        var patch = new FabricCapacityPatch
        {
            Sku = new FabricSku(tier, FabricSkuTier.Fabric)
        };

        return await StartOperationAsync(
            ct => IsCapacityAtTierAsync(tier, ct),
            (waitUntil, ct) => FabricResource.Value.UpdateAsync(waitUntil, patch, ct),
            onStartHandler,
            cancellationToken
        );
    }

    public virtual async Task<bool> SuspendAsync(
                Func<Task> onStartHandler = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        return await StartOperationAsync(
            ct => IsCapacityInStateAsync(FabricResourceState.Suspended, ct),
            FabricResource.Value.SuspendAsync,
            onStartHandler,
            cancellationToken
        );
    }

    private static bool IsConfiguredAzureResource(AzureResource azure)
    {
        if (azure is null)
        {
            return false;
        }

        var hasResource =
            !string.IsNullOrWhiteSpace(azure.SubscriptionId)
            && !string.IsNullOrWhiteSpace(azure.ResourceGroupName)
            && !string.IsNullOrWhiteSpace(azure.ResourceName);

        var hasClientSecret =
            !string.IsNullOrWhiteSpace(azure.TenantId)
            && !string.IsNullOrWhiteSpace(azure.ClientId)
            && !string.IsNullOrWhiteSpace(azure.ClientSecret);

        var hasManagedIdentity = !string.IsNullOrWhiteSpace(azure.ManagedIdentityClientId);

        return hasResource && (hasClientSecret || hasManagedIdentity);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(NotConfiguredMessage);
        }
    }

    private bool IsCapacityAtTier(string tier, CancellationToken cancellationToken = default)
    {
        var serverData = GetCapacityData(cancellationToken);
        return string.Equals(serverData.Sku?.Name, tier, StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<bool> IsCapacityAtTierAsync(string tier, CancellationToken cancellationToken = default)
    {
        var serverData = await GetCapacityDataAsync(cancellationToken);
        return string.Equals(serverData.Sku?.Name, tier, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCapacityInState(FabricResourceState state, CancellationToken cancellationToken = default)
    {
        var serverData = GetCapacityData(cancellationToken);
        return serverData.Properties.State == state;
    }

    private async ValueTask<bool> IsCapacityInStateAsync(FabricResourceState state, CancellationToken cancellationToken = default)
    {
        var serverData = await GetCapacityDataAsync(cancellationToken);
        return serverData.Properties.State == state;
    }

    private bool StartOperation<TOperation>(
        Func<CancellationToken, bool> isAlreadySatisfied,
        Func<WaitUntil, CancellationToken, TOperation> operation,
        Action onStartHandler = null,
        CancellationToken cancellationToken = default
    ) where TOperation : Operation
    {
        EnsureConfigured();

        if (isAlreadySatisfied(cancellationToken))
        {
            return true;
        }

        using (_semaphore.Lock(cancellationToken))
        {
            if (isAlreadySatisfied(cancellationToken))
            {
                return true;
            }

            var result = operation(WaitUntil.Started, cancellationToken);
            _memoryCache.Remove(CapacityDataCacheKey);

            if (onStartHandler is not null)
            {
                onStartHandler();
            }

            result.WaitForCompletionResponse(TimeSpan.FromSeconds(5), cancellationToken);
            _memoryCache.Remove(CapacityDataCacheKey);

            return result.HasCompleted;
        }
    }

    private async Task<bool> StartOperationAsync<TOperation>(
        Func<CancellationToken, ValueTask<bool>> isAlreadySatisfied,
        Func<WaitUntil, CancellationToken, Task<TOperation>> operation,
        Func<Task> onStartHandler = null,
        CancellationToken cancellationToken = default
    ) where TOperation : Operation
    {
        EnsureConfigured();

        if (await isAlreadySatisfied(cancellationToken))
        {
            return true;
        }

        using (await _semaphore.LockAsync(cancellationToken))
        {
            if (await isAlreadySatisfied(cancellationToken))
            {
                return true;
            }

            var result = await operation(WaitUntil.Started, cancellationToken);
            _memoryCache.Remove(CapacityDataCacheKey);

            if (onStartHandler is not null)
            {
                await onStartHandler().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            await result.WaitForCompletionResponseAsync(TimeSpan.FromSeconds(5), cancellationToken);
            _memoryCache.Remove(CapacityDataCacheKey);

            return result.HasCompleted;
        }
    }
}