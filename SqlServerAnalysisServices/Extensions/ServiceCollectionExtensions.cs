using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SqlServerAnalysisServices.Common;

namespace SqlServerAnalysisServices.Extensions;

public static class ServiceCollectionExtensions
{
    public static OptionsBuilder<TSettings> TryAddOptions<TSettings>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isRequired = true)
        where TSettings : class, IConfigurationSetting
    {
        return services.TryAddOptionsCore<TSettings>(builder =>
        {
            var section = isRequired ?
                configuration.GetRequiredSection(TSettings.ConfigurationKey) :
                configuration.GetSection(TSettings.ConfigurationKey);

            builder.Bind(section);
        });
    }

    private static OptionsBuilder<TSettings> TryAddOptionsCore<TSettings>(
        this IServiceCollection services,
        Action<OptionsBuilder<TSettings>> bindAction)
        where TSettings : class, IConfigurationSetting
    {
        var configureOptionsServiceType = typeof(IConfigureOptions<TSettings>);
        var validateOptionsServiceType = typeof(IValidateOptions<TSettings>);
        var count = services.Count;

        for (var i = 0; i < count; i++)
        {
            var descriptor = services[i];

            if (descriptor.ServiceType == configureOptionsServiceType || descriptor.ServiceType == validateOptionsServiceType)
                // Already added – return a "dummy" builder so caller can still chain if they want.
                return new OptionsBuilder<TSettings>(services, name: null);
        }

        var builder = services.AddOptions<TSettings>();
        bindAction(builder);

        return builder
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}