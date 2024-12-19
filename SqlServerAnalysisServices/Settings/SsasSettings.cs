using SqlServerAnalysisServices.Common;

namespace SqlServerAnalysisServices.Settings;

public record SsasSettings : IConfigurationSetting
{
    public static string ConfigurationKey => "Ssas";
    public string ConnectionString { get; init; }
}