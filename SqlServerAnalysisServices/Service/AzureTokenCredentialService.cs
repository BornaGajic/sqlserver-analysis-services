using Azure.Core;
using Azure.Identity;
using SqlServerAnalysisServices.Model;
using System.Runtime.Caching;

namespace SqlServerAnalysisServices.Service;

public class AzureTokenCredentialService
{
    private readonly MemoryCache _credentialCache = new MemoryCache(nameof(AzureTokenCredentialService));

    public TokenCredential GetAzureTokenCredential(AzureResource azureResource)
    {
        ArgumentNullException.ThrowIfNull(azureResource);

        var credentialList = new List<TokenCredential>
        {
            GetClientSecretCredential(azureResource.TenantId, azureResource.ClientId, azureResource.ClientSecret),
            GetManagedIdentityCredential(azureResource.ManagedIdentityClientId),
            GetDefaultCredential()
            //GetUsernamePasswordCredentials(azureResource.Username, azureResource.Password, azureResource.TenantId, azureResource.ClientId)
        };

        if (credentialList.Count == 0)
        {
            throw new Exception("Cannot create a TokenCredential instance - provide correct Azure values.");
        }

        return new ChainedTokenCredential([.. credentialList.Where(cred => cred is not null)]);
    }

    private ClientSecretCredential GetClientSecretCredential(string tenantId, string clientId, string clientSecret)
    {
        var clientSecretCacheKey = $"{tenantId}:{clientId}:{clientSecret}";
        if (
            !string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret)
        )
        {
            if (_credentialCache.Get(clientSecretCacheKey) is not ClientSecretCredential clientSecretCredential)
            {
                clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                _credentialCache.Set(clientSecretCacheKey, clientSecretCredential, ObjectCache.InfiniteAbsoluteExpiration);
            }

            return clientSecretCredential;
        }

        return null;
    }

    /*
        Picks up env vars:
            * AZURE_TENANT_ID
            * AZURE_CLIENT_ID (also works with MI; unset: system assigned, set: user assigned)
            * AZURE_CLIENT_SECRET
     */

    private DefaultAzureCredential GetDefaultCredential()
    {
        const string defaultAzureCredentialCacheKey = "default";

        if (_credentialCache.Get(defaultAzureCredentialCacheKey) is not DefaultAzureCredential defaultAzureCredential)
        {
            defaultAzureCredential = new DefaultAzureCredential();
            _credentialCache.Set(defaultAzureCredentialCacheKey, defaultAzureCredential, ObjectCache.InfiniteAbsoluteExpiration);
        }

        return defaultAzureCredential;
    }

    private ManagedIdentityCredential GetManagedIdentityCredential(string managedIdentityClientId)
    {
        var managedIdentityCacheKey = managedIdentityClientId;

        if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
        {
            if (_credentialCache.Get(managedIdentityCacheKey) is not ManagedIdentityCredential managedIdentityCredential)
            {
                managedIdentityCredential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));
                _credentialCache.Set(managedIdentityCacheKey, managedIdentityCredential, ObjectCache.InfiniteAbsoluteExpiration);
            }

            return managedIdentityCredential;
        }

        return null;
    }

    // This shouldn't be used. Even if you have all the information (uid, pwd, tid, cid), it requires a client secret - but
    // there isn't a ctor param that accepts a client secret; which means that the only way for this to work is to make
    // Azure Application PUBLIC (by setting the "Allow public client flows" to Enabled) that way Client Secret becomes unnecessary.
    // This allows anyone with a username, password, tenant id and client id client id to authenticate.
    [Obsolete]
    private UsernamePasswordCredential GetUsernamePasswordCredentials(string username, string password, string tenantId, string clientId)
    {
        var usernamePasswordCacheKey = $"{username}:{password}:{tenantId}:{clientId}";

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var usernamePasswordCredential = _credentialCache.Get(usernamePasswordCacheKey) as UsernamePasswordCredential;

            if (usernamePasswordCredential is null)
            {
                usernamePasswordCredential = new UsernamePasswordCredential(username, password, tenantId, clientId);
                _credentialCache.Set(usernamePasswordCacheKey, usernamePasswordCredential, ObjectCache.InfiniteAbsoluteExpiration);
            }

            return usernamePasswordCredential;
        }

        return null;
    }
}