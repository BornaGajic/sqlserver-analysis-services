namespace Framework.Model;

public enum SsasDatabaseState
{
    /// <summary>
    /// The object and all its contained processable objects are processed.
    /// </summary>
    Processed,

    /// <summary>
    /// At least one contained object is not processed.
    /// </summary>
    PartiallyProcessed,

    /// <summary>
    /// The object and its child objects are not processed.
    /// </summary>
    Unprocessed
}