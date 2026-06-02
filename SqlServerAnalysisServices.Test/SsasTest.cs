using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Model;
using SqlServerAnalysisServices.Service;
using SqlServerAnalysisServices.Settings;
using SqlServerAnalysisServices.Startup;

namespace SqlServerAnalysisServices.Test;

public class SsasTest : TestSetup
{
    public SsasTest()
    {
        var config = SetupConfiguration<SsasTest>();
        Container = SetupContainer(svc =>
        {
            //svc.RegisterAnalysisServices<SsasSettings, FabricSsasFactory>(config); // Fabric SSAS or localhost
            svc.RegisterAnalysisServices(config); // Default, AAS or localhost

            // or

            svc.AddSsasInstance(config, "localhost", factory =>
            {
                return factory.WithConnection((cfg, svc) =>
                {
                    var settings = svc.GetRequiredService<IOptions<SsasSettings>>();
                    cfg.UsingConnectionString(settings.Value.ConnectionString, new AzureResource()); // add your localhost database here (or in appsettings/client-secret)
                })
                .Create();
            });
        });
    }

    public IServiceProvider Container { get; private set; }

    [RunnableInDebugOnly]
    public void T01()
    {
        var ssas = Container.GetRequiredKeyedService<ISsas>("localhost");

        var query = ssas.Query<Region>(Region.QueryRegion);

        var q =
            from item in query
            where item.RegionName == "California"
            select item;

        var qq = q.ToList();

        var cubes = ssas.GetDatabases().ToList();

        Console.Write(qq);
    }

    public class Region
    {
        public const string QueryRegion = """
        EVALUATE
            SELECTCOLUMNS(
                'Region',
                "RegionId", Region[Region Id],
                "RegionName", Region[Region Name]
            )
        """;

        public string RegionId { get; set; } = default!;
        public string RegionName { get; set; } = default!;
    }
}