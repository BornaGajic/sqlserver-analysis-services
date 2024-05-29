namespace Framework.Model;

/// <summary>
/// More info: https://learn.microsoft.com/en-us/openspecs/sql_server_protocols/ms-ssas/3b0b02b1-ca61-4673-87b1-03c893442a1d
/// </summary>
public record SsasLock
{
    /// <summary>
    /// The unique identifier of the lock, as a GUID.
    /// </summary>
    public Guid LOCK_ID { get; init; }

    public int LOCK_STATUS { get; init; }
    public Guid LOCK_TRANSACTION_ID { get; init; }

    /// <summary>
    /// A bitmask of lock types.
    /// </summary>
    public SsasLockType LOCK_TYPE { get; init; }

    public SsasSession Session { get; init; }

    /// <summary>
    /// The session ID.
    /// </summary>
    public int SPID { get; init; }
}