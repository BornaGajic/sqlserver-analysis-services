namespace SqlServerAnalysisServices.Model
{
    internal abstract record SsasRowCount
    {
        internal long RowCount { get; init; }
    }

    internal record SsasTableRowCount : SsasRowCount
    {
        public const string TableRowCountQuery = """
            SELECT
                [DIMENSION_CAPTION] AS [TableName],
                [DIMENSION_CARDINALITY] AS [RowCount]
            FROM [$SYSTEM].[MDSchema_Dimensions]
        """;
        internal string TableName { get; init; }
    }

    internal record SsasPartitionRowCount : SsasTableRowCount
    {
        internal const string PartitionRowCountQuery = """
            SELECT
                [DIMENSION_NAME] AS [TableName],
                [PARTITION_NAME] AS [PartitionName],
                [RECORDS_COUNT] AS [RowCount]
            FROM [$SYSTEM].[DISCOVER_STORAGE_TABLE_COLUMN_SEGMENTS]
            WHERE [COMPRESSION_TYPE] = 'C123'
        """;
        internal string PartitionName { get; init; }
    }
}