using Microsoft.Extensions.DependencyInjection;
using SqlServerAnalysisServices.Common;

namespace SqlServerAnalysisServices.Service;

public class DefaultSsasFactory : SsasFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DefaultSsasFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override ISsas Create() => ActivatorUtilities.CreateInstance<Ssas>(_serviceProvider, InitializeConnection());
}