namespace SqlServerAnalysisServices.Model;

internal record SsasPartitionDataSources
{
    public const string Query = """
        SELECT
            [Name] AS [PartitionName],
            [DataSourceID] AS [DataSourceId]
        FROM [$SYSTEM].[TMSCHEMA_PARTITIONS]
        WHERE [DATASOURCEID] <> 0
    """;

    public string PartitionName { get; init; }
    public int DataSourceId { get; init; }
}