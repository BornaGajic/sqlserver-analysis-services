namespace Framework.Model;

public record SsasPartitionDescription
{
    public string Id { get; init; }
    public string Name { get; init; }
    public DateTime ModifiedTime { get; init; }
    public DateTime RefreshedTime { get; init; }
    public long RowCount { get; init; }
    public long Size { get; init; }
}