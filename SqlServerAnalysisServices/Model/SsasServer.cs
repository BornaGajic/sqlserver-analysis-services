namespace SqlServerAnalysisServices.Model;

public record SsasServer
{
    public string Name { get; init; }
    public string Tier { get; init; }
    public string Location { get; init; }
    public string State { get; init; }
    public DateTime Created { get; init; }
    public string CreatedBy { get; init; }
    public DateTime LastModified { get; init; }
    public string LastModifiedBy { get; init; }
    public IEnumerable<string> Administrators { get; init; }
    public bool IsOnline => State == "Succeeded";
}