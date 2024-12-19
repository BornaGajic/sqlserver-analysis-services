namespace SqlServerAnalysisServices.Model;

public record SsasDataSource : SsasDataSourceDescription
{
    public const string Query = """
        SELECT [ID] AS [Id], [Name], [ConnectionString], [ImpersonationMode], [Account], [Password], [ModifiedTime], [MaxConnections]
        FROM [$SYSTEM].[TMSCHEMA_DATA_SOURCES]
    """;

    public int Id { get; init; }
    public string Password { get; init; }
    public ImpersonationMode ImpersionationMode { get; init; }
}