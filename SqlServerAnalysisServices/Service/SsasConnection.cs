using SqlServerAnalysisServices.Common;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data.Common;
using System.Text.RegularExpressions;
using SqlServerAnalysisServices.Service;
using SqlServerAnalysisServices.Model;
using Azure.Core;
using System.Runtime.Caching;

namespace SqlServerAnalysisServices.Service;

internal partial class SsasConnection : ISsasConnectionFactory, ISsasConnectionConfigurator
{
    private readonly AzureTokenCredentialService _azTokenService;

    // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/samples/TokenCache.md
    private readonly MemoryCache _credentialCache = new MemoryCache(nameof(SsasConnection));

    public SsasConnection(AzureTokenCredentialService azTokenService)
    {
        _azTokenService = azTokenService;
    }

    private enum AuthenticationType
    { Azure, Basic };

    internal AzureResource AzureResource { get; private set; }

    internal string DataSource => ConnectionStringBuilder.TryGetValue("Data Source", out var dataSource)
        ? dataSource as string : throw new Exception("'Data Source' is a required connection string property.");

    private AuthenticationType AuthType { get; set; } = AuthenticationType.Basic;
    private DbConnectionStringBuilder ConnectionStringBuilder { get; } = [];

    public virtual AdomdConnection Create()
    {
        var connection = new AdomdConnection(ConnectionStringBuilder.ConnectionString);

        if (connection.IsCloudAnalysisServices())
        {
            connection.AccessToken = GetAzureSsasAccessToken();
            connection.OnAccessTokenExpired = oldToken => GetAzureSsasAccessToken();
        }

        return connection;
    }

    /// <inheritdoc/>
    public virtual ISsasConnectionConfigurator UsingConnectionString(string connectionString, AzureResource azureResource)
    {
        azureResource ??= new();

        ConnectionStringBuilder.ConnectionString = connectionString;

        if (DataSource.StartsWith("asazure://"))
        {
            if (azureResource == new AzureResource())
            {
                throw new Exception($"Parameter '{nameof(azureResource)}' is empty. When configuring SSAS located on Azure you must provide '{azureResource}' information.");
            }

            if (ConnectionStringBuilder.ContainsKey("UID") || ConnectionStringBuilder.ContainsKey("User ID"))
            {
                azureResource = azureResource with
                {
                    Username = (ConnectionStringBuilder.TryGetValue("UID", out var userName) ? userName : ConnectionStringBuilder["User ID"]) as string
                };
            }

            if (ConnectionStringBuilder.ContainsKey("PWD") || ConnectionStringBuilder.ContainsKey("Password"))
            {
                azureResource = azureResource with
                {
                    Password = (ConnectionStringBuilder.TryGetValue("PWD", out var password) ? password : ConnectionStringBuilder["Password"]) as string
                };
            }

            AzureResource = azureResource;
            AuthType = AuthenticationType.Azure;
        }
        else
        {
            AzureResource = null;
            AuthType = AuthenticationType.Basic;
        }

        return this;
    }

    /// <summary>
    /// Creates an Access Token for Azure hosted Analysis Services. Reuses <see cref="TokenCredential"/> instances from the memory cache in order to utilize MSALs internal cache (see: <see cref="TokenCredential.GetToken(TokenRequestContext, CancellationToken)"/>)
    /// </summary>
    internal virtual Microsoft.AnalysisServices.AccessToken GetAzureSsasAccessToken(CancellationToken cancellation = default)
    {
        if (string.IsNullOrWhiteSpace(ConnectionStringBuilder["Data Source"] as string))
        {
            throw new Exception("Data Source property is unconfigured.");
        }

        var regexMatch = RegionFromDataSourceRegex().Match(ConnectionStringBuilder["Data Source"].ToString());

        if (!regexMatch.Success)
        {
            throw new Exception("""
            Invalid connection string.
            -------------------------
            Valid values for Azure Analysis Services include <protocol>://<region>/<servername> where protocol is string asazure or
            link when using a server name alias, region is the Uri where the server was created (for example, westus.asazure.windows.net),
            and servername is the name of your unique server within the region.
            """);
        }

        var region = regexMatch.Groups["Region"].Value;
        var resource = $"https://{region}.asazure.windows.net";
        var scope = $"{resource}/.default";

        var authenticationResult = GetAzureSsasTokenCredential().GetToken(new TokenRequestContext([scope]), cancellation);

        return new Microsoft.AnalysisServices.AccessToken(authenticationResult.Token, authenticationResult.ExpiresOn, this);
    }

    /// <summary>
    /// Returns <see cref="TokenCredential"/> used to retreive a new SSAS Azure access token. Configured for this specific <see cref="Ssas"/> instance.
    /// </summary>
    internal virtual TokenCredential GetAzureSsasTokenCredential() => _azTokenService.GetAzureTokenCredential(AzureResource);

    [GeneratedRegex(@"asazure:\/\/(?'Region'.*?)\.", RegexOptions.Compiled)]
    protected static partial Regex RegionFromDataSourceRegex();
}