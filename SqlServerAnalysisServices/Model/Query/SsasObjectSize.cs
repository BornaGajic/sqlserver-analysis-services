namespace SqlServerAnalysisServices.Model
{
    internal abstract record SSasObjectSize
    {
        public long Size { get; init; }
    }

    internal record SsasTableSize : SSasObjectSize
    {
        public const string TableSizeQuery = """
            SELECT
                [DIMENSION_NAME] AS [TableName],
                [DICTIONARY_SIZE] AS [Size]
            FROM [$SYSTEM].[DISCOVER_STORAGE_TABLE_COLUMNS]
            WHERE [DICTIONARY_SIZE] > 0
        """;
        public string TableName { get; init; }
    }

    internal record SsasPartitionSize : SsasTableSize
    {
        public const string PartitionSizeQuery = """
            SELECT
                [DIMENSION_NAME] AS [TableName],
                [PARTITION_NAME] AS [PartitionName],
                [USED_SIZE] AS [Size]
            FROM [$SYSTEM].[DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS]
            WHERE [USED_SIZE] > 0
        """;
        public string PartitionName { get; init; }
    }
}