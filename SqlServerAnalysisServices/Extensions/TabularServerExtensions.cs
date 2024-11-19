using Framework.Model;
using Microsoft.AnalysisServices.Tabular;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json.Serialization;

namespace Framework.Extensions;

internal static class TabularServerExtensions
{
    private static readonly MemoryCache Cache = new MemoryCache(nameof(TabularServerExtensions));

    private record AzureSsasResolver
    {
        public string CoreServerName { get; init; }
        public string ServerAddress { get; init; }
        public string ServerResource { get; init; }
        public string TenantId { get; init; }
    }

    private record AzureSsasResolverResponse
    {
        [JsonPropertyName("clusterFQDN")]
        public string ClusterFQDN { get; init; }

        [JsonPropertyName("coreServerName")]
        public string CoreServerName { get; init; }

        [JsonPropertyName("tenantId")]
        public string TenantId { get; init; }
    }

    public static async Task<string> SendAzureXmlaRequestAsync(this Server server, XmlaSoapRequest request, CancellationToken cancellationToken = default)
    {
        var dataSource = new DbConnectionStringBuilder { ConnectionString = server.ConnectionString }["Data Source"] as string;

        var azureResolver = await ResolveAzureServer(dataSource, cancellationToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.AccessToken.Token);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-xmlaserver", azureResolver.CoreServerName);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-xmlacaps-negotiation-flags", "0,0,0,0,1");

        var newDataSource = new UriBuilder(azureResolver.ServerAddress)
        {
            Path = "/webapi/xmla"
        };

        dataSource = newDataSource.ToString();

        using var xmlaRequest = new HttpRequestMessage(HttpMethod.Post, dataSource)
        {
            Content = new StringContent(request.Request, Encoding.UTF8, "text/xml")
        };

        using var xmlaResponse = await httpClient.SendAsync(xmlaRequest, cancellationToken);

        xmlaResponse.EnsureSuccessStatusCode();

        return await xmlaResponse.Content.ReadAsStringAsync(cancellationToken);
    }

    public static string SendLocalhostXmlaRequest(this Server server, XmlaSoapRequest request, CancellationToken cancellationToken = default)
    {
        // Server needs to be locked because the connection cannot be used while an XmlReader object is open.
        lock (server)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var stringReader = new StringReader(request.Request);
            using var xmlReader = server.SendXmlaRequest(Microsoft.AnalysisServices.XmlaRequestType.Undefined, stringReader);
            xmlReader.MoveToContent();

            cancellationToken.ThrowIfCancellationRequested();

            return xmlReader.ReadInnerXml();
        }
    }

    private static async ValueTask<AzureSsasResolver> ResolveAzureServer(string dataSource, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{nameof(ResolveAzureServer)}-{dataSource}";
        var cacheHit = Cache.Get(cacheKey) as AzureSsasResolver;

        if (cacheHit is not null)
        {
            return cacheHit;
        }

        var resolverURI = new UriBuilder(dataSource);
        var azureResourceName = new UriBuilder("https", resolverURI.Host).ToString().TrimEnd('/');
        var azureServerName = resolverURI.Path.Trim('/');
        resolverURI.Scheme = "https";
        resolverURI.Path = "/webapi/clusterResolve";

        var request = new HttpRequestMessage(HttpMethod.Post, resolverURI.ToString())
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { serverName = azureServerName }), Encoding.UTF8, "application/json")
        };

        using var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseObj = System.Text.Json.JsonSerializer.Deserialize<AzureSsasResolverResponse>(await response.Content.ReadAsStringAsync(cancellationToken));

        var azureResolver = new AzureSsasResolver
        {
            ServerAddress = new UriBuilder("https", responseObj.ClusterFQDN).ToString().TrimEnd('/'),
            CoreServerName = responseObj.CoreServerName,
            ServerResource = azureResourceName,
            TenantId = responseObj.TenantId
        };

        Cache.Set(cacheKey, azureResolver, new CacheItemPolicy
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });

        return azureResolver;
    }
}