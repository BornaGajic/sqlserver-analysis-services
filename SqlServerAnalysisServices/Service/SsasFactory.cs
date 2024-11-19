using Framework.Common;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.Extensions.DependencyInjection;
using SqlServerAnalysisServices.Service;

namespace Framework.Service;

public class SsasFactory : ISsasFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SsasFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    protected Action<ISsasConnectionConfigurator, IServiceProvider> ConnectionBuilderConfigurator { get; set; }

    public virtual ISsas Create()
    {
        var scope = _serviceProvider.CreateAsyncScope();

        var ssas = ActivatorUtilities.CreateInstance<Ssas>(scope.ServiceProvider, InitializeConnection());
        ssas.Disposed += (sender, args) => scope.Dispose();

        return ssas;
    }

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

    protected internal virtual AdomdConnection InitializeConnection()
    {
        if (ConnectionBuilderConfigurator is null)
            throw new Exception($"Connection is unconfigured. Call {nameof(WithConnection)}.");

        var ssasConnection = new SsasConnection(new AzureTokenService());

        using var connectionFactoryScope = _serviceProvider.CreateAsyncScope();

        ConnectionBuilderConfigurator(ssasConnection, connectionFactoryScope.ServiceProvider);

        return ssasConnection.Create();
    }
}