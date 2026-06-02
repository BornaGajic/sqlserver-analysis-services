using SqlServerAnalysisServices.Model;
using Microsoft.AnalysisServices.Tabular;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json.Serialization;
using System.Net.Http.Json;

namespace SqlServerAnalysisServices.Extensions;

internal static class TabularServerExtensions
{
    private static readonly MemoryCache Cache = new MemoryCache(nameof(TabularServerExtensions));

    private record CloudXmlaResolver
    {
        public string CoreServerName { get; init; }
        public string ServerAddress { get; init; }
        public string ServerResource { get; init; }
        public string TenantId { get; init; }
    }

    private record CloudXmlaResolverResponse
    {
        [JsonPropertyName("clusterFQDN")]
        public string ClusterFQDN { get; init; }

        [JsonPropertyName("coreServerName")]
        public string CoreServerName { get; init; }

        [JsonPropertyName("tenantId")]
        public string TenantId { get; init; }
    }

    public static async Task<string> SendCloudXmlaRequestAsync(this Server server, XmlaSoapRequest request, CancellationToken cancellationToken = default)
    {
        var dataSource = new DbConnectionStringBuilder { ConnectionString = server.ConnectionString }["Data Source"] as string;

        var cloudResolver = await ResolveCloudXmlaServer(dataSource, cancellationToken);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.AccessToken.Token);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-xmlaserver", cloudResolver.CoreServerName);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-xmlacaps-negotiation-flags", "0,0,0,0,1");

        var newDataSource = new UriBuilder(cloudResolver.ServerAddress)
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

    public static string SendXmlaRequestViaSdk(this Server server, XmlaSoapRequest request, CancellationToken cancellationToken = default)
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

    private static async ValueTask<CloudXmlaResolver> ResolveCloudXmlaServer(string dataSource, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{nameof(ResolveCloudXmlaServer)}-{dataSource}";
        var cacheHit = Cache.Get(cacheKey) as CloudXmlaResolver;

        if (cacheHit is not null)
        {
            return cacheHit;
        }

        var resolverURI = new UriBuilder(dataSource);
        var cloudResourceName = new UriBuilder("https", resolverURI.Host).ToString().TrimEnd('/');
        var cloudServerName = resolverURI.Path.Trim('/');
        resolverURI.Scheme = "https";
        resolverURI.Path = "/webapi/clusterResolve";

        var request = new HttpRequestMessage(HttpMethod.Post, resolverURI.ToString())
        {
            Content = JsonContent.Create(new { serverName = cloudServerName })
        };

        using var httpClient = new HttpClient();
        var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseObj = System.Text.Json.JsonSerializer.Deserialize<CloudXmlaResolverResponse>(await response.Content.ReadAsStringAsync(cancellationToken));

        var cloudResolver = new CloudXmlaResolver
        {
            ServerAddress = new UriBuilder("https", responseObj.ClusterFQDN).ToString().TrimEnd('/'),
            CoreServerName = responseObj.CoreServerName,
            ServerResource = cloudResourceName,
            TenantId = responseObj.TenantId
        };

        Cache.Set(cacheKey, cloudResolver, new CacheItemPolicy
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });

        return cloudResolver;
    }
}