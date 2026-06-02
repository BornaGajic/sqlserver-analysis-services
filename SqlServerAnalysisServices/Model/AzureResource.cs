namespace SqlServerAnalysisServices.Model;

public record AzureResource : AzureCredentialSettings
{
    public string Instance { get; init; }
    public string ResourceName { get; init; }
    public string Domain { get; init; }
    public string Audience { get; init; }
    public string Authority { get; init; }
    public string[] Scopes { get; init; } = [];
    public string ResourceGroupName { get; init; }
    public string SubscriptionId { get; init; }
    public string InviteRedirectUrl { get; init; }
    public string Username { get; init; }
    public string Password { get; init; }
}