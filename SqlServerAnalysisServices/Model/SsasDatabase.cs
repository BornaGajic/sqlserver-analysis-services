namespace SqlServerAnalysisServices.Model;

public record SsasDatabase
{
    public string Id { get; init; }
    public string Name { get; init; }
    public DateTime LastProcessedUtc { get; init; }
    public DateTime LastSchemaUpdateUtc { get; init; }
    public string Model { get; init; }
    public long Size { get; init; }
}