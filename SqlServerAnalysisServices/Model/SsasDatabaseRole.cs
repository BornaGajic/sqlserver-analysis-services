namespace SqlServerAnalysisServices.Model;

public record SsasDatabaseRole
{
    public string Name { get; init; }
    public string Description { get; init; }
    public SsasRolePermission Permission { get; init; }
}