namespace Framework.Model;

public record SsasDatabaseDescription
{
    public string Id { get; init; }
    public string Name { get; init; }
    public DateTime LastProcessed { get; init; }
    public string Model { get; init; }
    public long Size { get; init; }
    public ICollection<SsasTableDescription> Tables { get; init; }
    public long RowCount => Tables.Sum(table => table.RowCount);
}