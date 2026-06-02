using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Extensions;
using SqlServerAnalysisServices.Service;
using SqlServerAnalysisServices.Settings;
using SqlServerAnalysisServices.Utility;

namespace SqlServerAnalysisServices.Startup;

public static class SsasStartupExtension
{
    /// <summary>
    /// Adds keyed <see cref="ISsas"/> singleton; retreive it using <see cref="FromKeyedServicesAttribute"/>.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSettings>(this IServiceCollection services, IConfiguration configuration, string key, Func<ISsasFactory, ISsas> factory)
        where TSettings : SsasSettings, IConfigurationSetting
        => services.AddSsasInstance<TSettings, DefaultSsasFactory>(configuration, key, factory);

    /// <summary>
    /// Adds keyed <see cref="ISsas"/> singleton; retreive it using <see cref="FromKeyedServicesAttribute"/>.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSettings, TSsasFactory>(this IServiceCollection services, IConfiguration configuration, string key, Func<ISsasFactory, ISsas> factory)
        where TSettings : SsasSettings, IConfigurationSetting
        where TSsasFactory : SsasFactory
    {
        services.RegisterAnalysisServices<TSettings, TSsasFactory>(configuration);
        services.TryAddKeyedSingleton(key, (svc, key) => factory(svc.GetRequiredService<ISsasFactory>()));
        return services;
    }

    /// <summary>
    /// Adds keyed <see cref="ISsas"/> singleton; retreive it using <see cref="FromKeyedServicesAttribute"/>.
    /// </summary>
    public static IServiceCollection AddSsasInstance(this IServiceCollection services, IConfiguration configuration, string key, Func<ISsasFactory, ISsas> factory)
        => services.AddSsasInstance<SsasSettings>(configuration, key, factory);

    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSettings>(this IServiceCollection services, IConfiguration configuration, Func<ISsasFactory, ISsas> factory)
        where TSettings : SsasSettings, IConfigurationSetting
        => services.AddSsasInstance<TSettings, DefaultSsasFactory>(configuration, factory);

    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSettings, TSsasFactory>(this IServiceCollection services, IConfiguration configuration, Func<ISsasFactory, ISsas> factory)
        where TSettings : SsasSettings, IConfigurationSetting
        where TSsasFactory : SsasFactory
    {
        services.RegisterAnalysisServices<TSettings, TSsasFactory>(configuration);
        services.TryAddSingleton(svc => factory(svc.GetRequiredService<ISsasFactory>()));
        return services;
    }


    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance(this IServiceCollection services, IConfiguration configuration, Func<ISsasFactory, ISsas> factory)
        => services.AddSsasInstance<SsasSettings>(configuration, factory);

    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance(this IServiceCollection services, IConfiguration configuration)
        => services.AddSsasInstance<DefaultSsasFactory>(configuration);

    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSsasFactory>(this IServiceCollection services, IConfiguration configuration)
        where TSsasFactory : SsasFactory
    {
        return services.AddSsasInstance<SsasSettings, TSsasFactory>(configuration,
            (factory) =>
            {
                return factory.WithConnection((cfg, svc) =>
                {
                    var settings = svc.GetRequiredService<IOptions<SsasSettings>>().Value;
                    cfg.UsingConnectionString(settings.ConnectionString, settings.Azure);
                })
                .Create();
            });
    }

    /// <summary>
    /// 1. Registers <see cref="ISsasFactory"/> with <see cref="SsasFactory"/>
    /// </summary>
    public static IServiceCollection RegisterAnalysisServices(this IServiceCollection services, IConfiguration configuration)
        => services.RegisterAnalysisServices<SsasSettings>(configuration);

    /// <summary>
    /// 1. Registers <see cref="ISsasFactory"/> with <see cref="SsasFactory"/>
    /// </summary>
    public static IServiceCollection RegisterAnalysisServices<TSettings>(this IServiceCollection services, IConfiguration configuration)
        where TSettings : SsasSettings, IConfigurationSetting
        => services.RegisterAnalysisServices<TSettings, DefaultSsasFactory>(configuration);

    /// <summary>
    /// 1. Registers <see cref="ISsasFactory"/> with <typeparamref name="TSsasFactory"/>
    /// </summary>
    public static IServiceCollection RegisterAnalysisServices<TSettings, TSsasFactory>(this IServiceCollection services, IConfiguration configuration)
        where TSettings : SsasSettings, IConfigurationSetting
        where TSsasFactory : SsasFactory
    {
        services.RegisterSsasOptions<TSettings>(configuration);
        services.RegisterSsasServices<TSsasFactory>();
        return services;
    }

    private static OptionsBuilder<TSettings> RegisterSsasOptions<TSettings>(this IServiceCollection services, IConfiguration configuration)
        where TSettings : SsasSettings, IConfigurationSetting
    {
        return services.TryAddOptions<TSettings>(configuration);
    }

    private static IServiceCollection RegisterSsasServices<TSsasFactory>(this IServiceCollection services)
        where TSsasFactory : SsasFactory
    {
        SqlMapperUtility.TryAddSqlTypeHandlers();

        services.TryAddSingleton<ISsasFactory, TSsasFactory>();
        services.TryAddSingleton<FabricCapacityManager>();

        return services;
    }
}