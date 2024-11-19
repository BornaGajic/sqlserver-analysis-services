namespace Framework.Model;

public record SsasRoleMember
{
    /// <summary>
    /// The security name that identifies the user or group of the member.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// A string that defines the identity provider used for authentication.
    /// </summary>
    public string IdentityProvider { get; init; }

    public SsasDatabaseRole Role { get; init; }
}