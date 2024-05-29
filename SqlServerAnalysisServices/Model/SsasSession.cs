namespace Framework.Model;

/// <summary>
/// More info: https://learn.microsoft.com/en-us/openspecs/sql_server_protocols/ms-ssas/b85aa76d-d963-4f93-94c4-2ae6ea57f799
/// </summary>
public record SsasSession
{
    public string SESSION_CURRENT_DATABASE { get; init; }
    public int SESSION_SPID { get; init; }
}