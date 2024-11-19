namespace Framework.Model
{
    internal abstract record SSasObjectSize
    {
        public long Size { get; init; }
    }

    internal record SsasTableSize : SSasObjectSize
    {
        public string TableName { get; init; }
    }

    internal record SsasPartitionSize : SsasTableSize
    {
        public string PartitionName { get; init; }
    }
}