using Framework.Model;

namespace Framework.Common;

public interface ISsasConnectionFactoryConfigurator
{
    string ConnectionString { get; }

    /// <summary>
    /// Specifies whether a local password is to be used to encrypt local cubes.
    /// </summary>
    ISsasConnectionFactoryConfigurator UsePasswordEncryption(bool usePasswordEncryption);

    ISsasConnectionFactoryConfigurator UsingConnectionString(string connectionString);

    /// <summary>
    /// Cube name or perspective name. Default: "Model".
    /// </summary>
    ISsasConnectionFactoryConfigurator WithCube(string cube);

    /// <summary>
    /// Catalog name.
    /// </summary>
    ISsasConnectionFactoryConfigurator WithDatabase(string database);

    /// <summary>
    /// Specifies the server instance.
    /// </summary>
    ISsasConnectionFactoryConfigurator WithDataSource(string dataSource);

    /// <summary>
    /// Use when an user identity must be impersonated on the server. For SSAS, specify in a domain\user format.
    /// For Azure AS and Power BI Premium, specify in UPN format. To use this property, the caller must have administrative permissions in Analysis Services.
    /// In Power BI Premium, the caller must be a workspace admin where the semantic model is located.
    /// </summary>
    ISsasConnectionFactoryConfigurator WithEffectiveUserName(string effectiveUserName);

    /// <summary>
    /// Indicates the level of impersonation that the server is allowed to use when impersonating the client
    /// </summary>
    ISsasConnectionFactoryConfigurator WithImpersonationLevel(ImpersonationLevel impersonation);
}