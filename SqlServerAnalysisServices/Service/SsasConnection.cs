using SqlServerAnalysisServices.Common;
using System.Data.Common;
using System.Text.RegularExpressions;
using SqlServerAnalysisServices.Model;
using Azure.Core;
using SqlServerAnalysisServices.Extensions;

namespace SqlServerAnalysisServices.Service;

public partial class SsasConnection : ISsasConnectionConfigurator
{
    private readonly AzureTokenCredentialService _azTokenService;

    public SsasConnection(AzureTokenCredentialService azTokenService)
    {
        _azTokenService = azTokenService;
    }

    internal AzureResource AzureResource { get; private set; }

    internal string ConnectionString => ConnectionStringBuilder.ConnectionString;

    internal string DataSource => ConnectionStringBuilder.TryGetValue("Data Source", out var dataSource)
        ? dataSource as string : throw new Exception("'Data Source' is a required connection string property.");

    private DbConnectionStringBuilder ConnectionStringBuilder { get; } = [];

    /// <inheritdoc/>
    public virtual ISsasConnectionConfigurator UsingConnectionString(string connectionString, AzureResource azureResource)
    {
        azureResource ??= new();

        ConnectionStringBuilder.ConnectionString = connectionString;

        if (IsCloudAnalysisServices())
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
                ConnectionStringBuilder.Remove("PWD");
                ConnectionStringBuilder.Remove("Password");
            }

            AzureResource = azureResource;
        }
        else
        {
            AzureResource = null;
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

        AccessToken token;

        if (IsFabricPowerBIEndpoint())
        {
            token = GetAzureSsasTokenCredential().GetPowerBiToken(cancellation);
        }
        else
        {
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
            token = GetAzureSsasTokenCredential().GetAnalysisServicesToken(region, cancellation);
        }

        return new Microsoft.AnalysisServices.AccessToken(token.Token, token.ExpiresOn, this);
    }

    /// <summary>
    /// Returns <see cref="TokenCredential"/> used to retreive a new SSAS Azure access token. Configured for this specific <see cref="Ssas"/> instance.
    /// </summary>
    internal virtual TokenCredential GetAzureSsasTokenCredential() => _azTokenService.GetAzureTokenCredential(AzureResource);

    internal bool IsAzureAnalysisServices()
    {
        return DataSource?.StartsWith("asazure://", StringComparison.OrdinalIgnoreCase) == true;
    }

    internal bool IsCloudAnalysisServices()
    {
        return IsAzureAnalysisServices() || IsFabricPowerBIEndpoint();
    }

    internal bool IsFabricPowerBIEndpoint()
    {
        return DataSource?.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase) == true;
    }

    [GeneratedRegex(@"asazure:\/\/(?'Region'.*?)\.", RegexOptions.Compiled)]
    protected static partial Regex RegionFromDataSourceRegex();
}