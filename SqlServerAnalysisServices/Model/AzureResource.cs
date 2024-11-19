namespace SqlServerAnalysisServices.Model;

public record AzureResource
{
    public string TenantId { get; init; }
    public string ClientId { get; init; }
    public string ClientSecret { get; init; }
    public string Instance { get; init; }
    public string Domain { get; init; }
    public string Audience { get; init; }
    public string[] Scopes { get; init; } = [];
    public string ManagedIdentityClientId { get; init; }
    public string ResourceGroupName { get; init; }
    public string SubscriptionId { get; init; }
    public string Username { get; init; }
    public string Password { get; init; }
}