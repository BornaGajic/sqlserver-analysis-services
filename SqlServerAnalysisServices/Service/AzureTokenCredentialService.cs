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
            GetUsernamePasswordCredentials(azureResource.Username, azureResource.Password, azureResource.TenantId, azureResource.ClientId)
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
            var clientSecretCredential = _credentialCache.Get(clientSecretCacheKey) as ClientSecretCredential;

            if (clientSecretCredential is null)
            {
                clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                _credentialCache.Set(clientSecretCacheKey, clientSecretCredential, ObjectCache.InfiniteAbsoluteExpiration);
            }

            return clientSecretCredential;
        }

        return null;
    }

    private ManagedIdentityCredential GetManagedIdentityCredential(string managedIdentityClientId)
    {
        var managedIdentityCacheKey = managedIdentityClientId;

        if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
        {
            var managedIdentityCredential = _credentialCache.Get(managedIdentityCacheKey) as ManagedIdentityCredential;

            if (managedIdentityCredential is null)
            {
                managedIdentityCredential = new ManagedIdentityCredential(managedIdentityClientId);
                _credentialCache.Set(managedIdentityCacheKey, managedIdentityCredential, ObjectCache.InfiniteAbsoluteExpiration);
            }

            return managedIdentityCredential;
        }

        return null;
    }

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