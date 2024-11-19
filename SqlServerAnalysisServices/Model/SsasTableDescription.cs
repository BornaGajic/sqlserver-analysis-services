namespace Framework.Model;

public record SsasTableDescription
{
    public string Id { get; init; }
    public ICollection<SsasPartitionDescription> Partitions { get; init; }
    public string Name { get; init; }
    public DateTime ModifiedTime { get; init; }
    public DateTime StructureModifiedTime { get; init; }
    public long RowCount { get; init; }
    public long Size { get; init; }
}