using SqlServerAnalysisServices.Model;

namespace SqlServerAnalysisServices.Extensions;

public static class AzureResourceExtensions
{
    /// <summary>
    /// True when a client secret credential can be built: <see cref="AzureCredentialSettings.TenantId"/>,
    /// <see cref="AzureCredentialSettings.ClientId"/> and <see cref="AzureCredentialSettings.ClientSecret"/> are all present.
    /// </summary>
    public static bool IsValidClient(this AzureResource azure) =>
        azure is not null
        && !string.IsNullOrWhiteSpace(azure.TenantId)
        && !string.IsNullOrWhiteSpace(azure.ClientId)
        && !string.IsNullOrWhiteSpace(azure.ClientSecret);

    /// <summary>
    /// True when a managed identity credential can be built: <see cref="AzureCredentialSettings.ManagedIdentityClientId"/> is present.
    /// </summary>
    public static bool IsValidManagedIdentity(this AzureResource azure) =>
        azure is not null
        && !string.IsNullOrWhiteSpace(azure.ManagedIdentityClientId);

    /// <summary>
    /// True when the resource coordinates are present: <see cref="AzureResource.SubscriptionId"/>,
    /// <see cref="AzureResource.ResourceGroupName"/> and <see cref="AzureResource.ResourceName"/>.
    /// </summary>
    public static bool IsValidResource(this AzureResource azure) =>
        azure is not null
        && !string.IsNullOrWhiteSpace(azure.SubscriptionId)
        && !string.IsNullOrWhiteSpace(azure.ResourceGroupName)
        && !string.IsNullOrWhiteSpace(azure.ResourceName);

    /// <summary>
    /// Validates resource coordinates and authentication, returning a result that lists what is missing
    /// per category. Valid when the resource coordinates are present and it can authenticate via a client
    /// secret credential or a managed identity.
    /// </summary>
    public static AzureResourceValidation Validate(this AzureResource azure)
    {
        var errors = new List<string>();

        if (azure is null)
        {
            errors.Add("Azure resource settings are missing.");
            return new AzureResourceValidation(errors);
        }

        var missingResource = MissingFields(
            (nameof(azure.SubscriptionId), azure.SubscriptionId),
            (nameof(azure.ResourceGroupName), azure.ResourceGroupName),
            (nameof(azure.ResourceName), azure.ResourceName)
        );

        if (missingResource.Count > 0)
            errors.Add($"Resource is incomplete - missing {string.Join(", ", missingResource)}.");

        if (!azure.IsValidClient() && !azure.IsValidManagedIdentity())
        {
            var missingClient = MissingFields(
                (nameof(azure.TenantId), azure.TenantId),
                (nameof(azure.ClientId), azure.ClientId),
                (nameof(azure.ClientSecret), azure.ClientSecret)
            );

            errors.Add(
                "Authentication is incomplete - provide either a client credential "
                + $"(missing {string.Join(", ", missingClient)}) or a managed identity "
                + $"(missing {nameof(azure.ManagedIdentityClientId)})."
            );
        }

        return new AzureResourceValidation(errors);
    }

    private static List<string> MissingFields(params (string Name, string Value)[] fields)
    {
        var missing = new List<string>();

        foreach (var (name, value) in fields)
            if (string.IsNullOrWhiteSpace(value))
                missing.Add(name);

        return missing;
    }
}