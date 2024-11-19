namespace Framework.Common;

public interface ISsasFactory
{
    ISsas Create();

    ISsasFactory WithConnection(Action<ISsasConnectionConfigurator, IServiceProvider> connectionBuilder);

    ISsasFactory WithConnection(Action<ISsasConnectionConfigurator> connectionBuilder);
}