using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Service;
using SqlServerAnalysisServices.Settings;

namespace SqlServerAnalysisServices.Startup;

public static class SsasStartupExtension
{
    /// <summary>
    /// Adds keyed <see cref="ISsas"/> singleton; retreive it using <see cref="FromKeyedServicesAttribute"/>.
    /// </summary>
    public static IServiceCollection AddSsasInstance<TSettings>(this IServiceCollection services, IConfiguration configuration, string key, Func<ISsasFactory, ISsas> factory)
        where TSettings : SsasSettings, IConfigurationSetting
    {
        services.RegisterAnalysisServices<TSettings>(configuration);
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
    {
        services.RegisterAnalysisServices<TSettings>(configuration);
        services.TryAddSingleton(svc => factory(svc.GetRequiredService<ISsasFactory>()));
        return services;
    }


    /// <summary>
    /// Adds <see cref="ISsas"/> singleton.
    /// </summary>
    public static IServiceCollection AddSsasInstance(this IServiceCollection services, IConfiguration configuration, Func<ISsasFactory, ISsas> factory)
        => services.AddSsasInstance<SsasSettings>(configuration, factory);

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
    {
        services.RegisterSsasOptions<TSettings>(configuration);
        services.RegisterSsasServices();
        return services;
    }

    private static OptionsBuilder<TSettings> RegisterSsasOptions<TSettings>(this IServiceCollection services, IConfiguration configuration)
        where TSettings : SsasSettings, IConfigurationSetting
    {
        return services.AddOptions<TSettings>()
            .Bind(configuration.GetRequiredSection(TSettings.ConfigurationKey))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static IServiceCollection RegisterSsasServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ISsasFactory, SsasFactory>();

        return services;
    }
}