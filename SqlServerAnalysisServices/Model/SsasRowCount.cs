namespace Framework.Model
{
    internal abstract record SsasRowCount
    {
        internal long RowCount { get; init; }
    }

    internal record SsasTableRowCount : SsasRowCount
    {
        internal string TableName { get; init; }
    }

    internal record SsasPartitionRowCount : SsasTableRowCount
    {
        internal string PartitionName { get; init; }
    }
}
