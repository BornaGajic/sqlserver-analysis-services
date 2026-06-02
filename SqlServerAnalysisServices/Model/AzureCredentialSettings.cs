namespace SqlServerAnalysisServices.Model;

public record AzureCredentialSettings
{
    public string TenantId { get; init; }
    public string ClientId { get; init; }
    public string ClientSecret { get; init; }
    public string ManagedIdentityClientId { get; init; }
}