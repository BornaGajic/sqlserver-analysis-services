namespace SqlServerAnalysisServices.Model;

public record SsasDataSourceDescription
{
    public string Name { get; init; }
    public string Description { get; init; }
    public DateTime ModifiedTime { get; init; }
    public string Account { get; init; }
    public int MaxConnections { get; init; }
    public string ConnectionString { get; init; }
}