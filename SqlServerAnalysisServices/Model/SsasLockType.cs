namespace Framework.Model;

/// <summary>
/// More info: https://learn.microsoft.com/en-us/openspecs/sql_server_protocols/ms-ssas/566ef60a-3491-4a21-9b01-caad1365fdf3
/// </summary>
public enum SsasLockType
{
    /// <summary>
    /// No lock
    /// </summary>
    LOCK_NONE = 0x0000000,

    /// <summary>
    /// Inactive session; does not interfere with other locks.
    /// </summary>
    LOCK_SESSION_LOCK = 0x0000001,

    /// <summary>
    /// Read lock during processing.
    /// </summary>
    LOCK_READ = 0x0000002,

    /// <summary>
    /// Write lock during processing.
    /// </summary>
    LOCK_WRITE = 0x0000004,

    /// <summary>
    /// Commit lock, shared.
    /// </summary>
    LOCK_COMMIT_READ = 0x0000008,

    /// <summary>
    /// Commit lock, exclusive.
    /// </summary>
    LOCK_COMMIT_WRITE = 0x0000010,

    /// <summary>
    /// Abort at commit progress.
    /// </summary>
    LOCK_COMMIT_ABORTABLE = 0x0000020,

    /// <summary>
    /// Commit in progress.
    /// </summary>
    LOCK_COMMIT_INPROGRESS = 0x0000040,

    /// <summary>
    /// Invalid lock.
    /// </summary>
    LOCK_INVALID = 0x0000080
}