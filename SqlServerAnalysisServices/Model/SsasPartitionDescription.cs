namespace SqlServerAnalysisServices.Model;

public record SsasPartitionDescription
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public DateTime ModifiedTime { get; init; }
    public DateTime RefreshedTime { get; init; }
    public long RowCount { get; init; }
    public long Size { get; init; }
    public ICollection<SsasDataSourceDescription> DataSources { get; init; }
}