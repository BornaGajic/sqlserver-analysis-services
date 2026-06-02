using Microsoft.Extensions.DependencyInjection;
using SqlServerAnalysisServices.Common;

namespace SqlServerAnalysisServices.Service;

public class FabricSsasFactory : SsasFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FabricSsasFactory(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override ISsas Create() => ActivatorUtilities.CreateInstance<FabricSsas>(_serviceProvider, InitializeConnection());
}