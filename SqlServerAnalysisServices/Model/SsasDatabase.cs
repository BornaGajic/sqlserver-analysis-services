namespace Framework.Model;

public record SsasDatabase
{
    public string DatabaseId { get; set; }
    public string DatabaseName { get; set; }
    public string Description { get; set; }
    public bool IsProcessing { get; set; }
    public DateTime LastProcessedUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public string Model { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }
}