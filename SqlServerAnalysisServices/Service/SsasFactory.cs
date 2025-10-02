using SqlServerAnalysisServices.Common;
using Microsoft.Extensions.DependencyInjection;

namespace SqlServerAnalysisServices.Service;

public class SsasFactory : ISsasFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SsasFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    protected Action<ISsasConnectionConfigurator, IServiceProvider> ConnectionBuilderConfigurator { get; set; }

    public virtual ISsas Create() => ActivatorUtilities.CreateInstance<Ssas>(_serviceProvider, InitializeConnection());

    public virtual ISsasFactory WithConnection(Action<ISsasConnectionConfigurator, IServiceProvider> builder)
    {
        ConnectionBuilderConfigurator = builder;
        return this;
    }

    public virtual ISsasFactory WithConnection(Action<ISsasConnectionConfigurator> builder)
    {
        ConnectionBuilderConfigurator = (connectionBuilder, _) => builder(connectionBuilder);
        return this;
    }

    protected virtual SsasConnection InitializeConnection()
    {
        if (ConnectionBuilderConfigurator is null)
            throw new Exception($"Connection is unconfigured. Call {nameof(WithConnection)}.");

        var ssasConnection = new SsasConnection(new AzureTokenCredentialService());

        using var connectionFactoryScope = _serviceProvider.CreateAsyncScope();

        ConnectionBuilderConfigurator(ssasConnection, connectionFactoryScope.ServiceProvider);

        return ssasConnection;
    }
}