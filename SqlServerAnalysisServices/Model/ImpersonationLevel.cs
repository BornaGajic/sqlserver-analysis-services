namespace Framework.Model;

public enum ImpersonationLevel
{
    /// <summary>
    /// The client is anonymous to the server. The server process cannot obtain information about the client, nor can the client be impersonated.
    /// </summary>
    Anonymous,

    /// <summary>
    /// The server process can get the client identity. The server can impersonate the client identity for authorization purposes but cannot access system objects as the client.
    /// </summary>
    Identify,

    /// <summary>
    /// This is the default value. The client identity can be impersonated, but only when the connection is established, and not on every call.
    /// </summary>
    Impersonate,

    /// <summary>
    /// The server process can impersonate the client security context while acting on behalf of the client. The server process can also make outgoing calls to other servers while acting on behalf of the client.
    /// </summary>
    Delegate
}