namespace Framework.Common;

public interface ISsasFactory
{
    ISsas Create();

    /// <summary>
    /// Any service created through <see cref="IServiceProvider"/> will be disposed when the created <see cref="Ssas"/> instance gets disposed.
    /// </summary>
    ISsasFactory WithConnection(Action<ISsasConnectionFactoryConfigurator, IServiceProvider> connectionBuilder);

    ISsasFactory WithConnection(Action<ISsasConnectionFactoryConfigurator> connectionBuilder);
}