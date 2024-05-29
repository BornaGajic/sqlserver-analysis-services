using Framework.Common;
using Microsoft.AnalysisServices.AdomdClient;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace Framework.Service
{
    public class SsasFactory : ISsasFactory
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly SsasConnectionFactory _ssasConnectionFactory = new();

        public SsasFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected Action<ISsasConnectionFactoryConfigurator, IServiceProvider> ConnectionBuilderConfigurator { get; set; }

        protected virtual ISsasConnectionFactory SsasConnectionFactory => _ssasConnectionFactory;
        protected virtual ISsasConnectionFactoryConfigurator SsasConnectionFactoryConfigurator => _ssasConnectionFactory;

        public virtual ISsas Create()
        {
            var scope = Initialize(out var server, out var connection);

            var ssas = ActivatorUtilities.CreateInstance<Ssas>(scope.ServiceProvider, server, connection);
            ssas.Disposed += (sender, args) => scope.Dispose();

            return ssas;
        }

        public virtual ISsasFactory WithConnection(Action<ISsasConnectionFactoryConfigurator, IServiceProvider> builder)
        {
            ConnectionBuilderConfigurator = builder;
            return this;
        }

        public virtual ISsasFactory WithConnection(Action<ISsasConnectionFactoryConfigurator> builder)
        {
            ConnectionBuilderConfigurator = (connectionBuilder, _) => builder(connectionBuilder);
            return this;
        }

        protected internal virtual IServiceScope Initialize(out Server server, out AdomdConnection adomdConnection)
        {
            if (ConnectionBuilderConfigurator is null)
            {
                throw new Exception($"Connection is unconfigured. Call {nameof(WithConnection)}.");
            }

            var ssasConnectionFactoryConfigurator = SsasConnectionFactoryConfigurator;

            var scope = _serviceProvider.CreateScope();

            ConnectionBuilderConfigurator(ssasConnectionFactoryConfigurator, scope.ServiceProvider);

            adomdConnection = SsasConnectionFactory.Create(ssasConnectionFactoryConfigurator.ConnectionString);

            server = new();
            server.Connect(adomdConnection.ConnectionString);

            if (adomdConnection.State is ConnectionState.Closed)
            {
                adomdConnection.Open();
            }

            return scope;
        }
    }
}