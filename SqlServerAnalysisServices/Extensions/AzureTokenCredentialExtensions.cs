using Azure.Core;
using SqlServerAnalysisServices.Model;

namespace SqlServerAnalysisServices.Extensions;

public static class AzureTokenCredentialExtensions
{
    public static AccessToken GetAnalysisServicesToken(this TokenCredential credential, string region, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var scope = AzureScope.AnalysisServices.Replace("{region}", region);
        return credential.GetToken(new TokenRequestContext([scope]), cancellationToken);
    }

    public static ValueTask<AccessToken> GetAnalysisServicesTokenAsync(this TokenCredential credential, string region, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        var scope = AzureScope.AnalysisServices.Replace("{region}", region);
        return credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken);
    }

    public static AccessToken GetGraphToken(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetToken(new TokenRequestContext([AzureScope.Graph]), cancellationToken);

    public static ValueTask<AccessToken> GetGraphTokenAsync(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetTokenAsync(new TokenRequestContext([AzureScope.Graph]), cancellationToken);

    public static AccessToken GetManagementToken(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetToken(new TokenRequestContext([AzureScope.Management]), cancellationToken);

    public static ValueTask<AccessToken> GetManagementTokenAsync(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetTokenAsync(new TokenRequestContext([AzureScope.Management]), cancellationToken);

    public static AccessToken GetPowerBiToken(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetToken(new TokenRequestContext([AzureScope.PowerBiAnalysis]), cancellationToken);

    public static ValueTask<AccessToken> GetPowerBiTokenAsync(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetTokenAsync(new TokenRequestContext([AzureScope.PowerBiAnalysis]), cancellationToken);

    public static AccessToken GetSqlToken(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetToken(new TokenRequestContext([AzureScope.SqlDatabase]), cancellationToken);

    public static ValueTask<AccessToken> GetSqlTokenAsync(this TokenCredential credential, CancellationToken cancellationToken = default)
        => credential.GetTokenAsync(new TokenRequestContext([AzureScope.SqlDatabase]), cancellationToken);
}