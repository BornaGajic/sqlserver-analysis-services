using SqlServerAnalysisServices.Common;
using SqlServerAnalysisServices.Model;

namespace SqlServerAnalysisServices.Settings;

public record SsasSettings : IConfigurationSetting
{
    public static string ConfigurationKey => "Ssas";
    public string ConnectionString { get; init; }
    public AzureResource Azure { get; init; }
}