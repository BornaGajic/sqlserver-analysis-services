using SqlServerAnalysisServices.Model;

namespace Framework.Common;

public interface ISsasConnectionConfigurator
{
    /// <summary>
    /// In case of an Azure hosted SSAS server provide the <paramref name="azureResource"/> otherwise <paramref name="azureResource"/> can be null or an empty object.
    /// </summary>
    ISsasConnectionConfigurator UsingConnectionString(string connectionString, AzureResource azureResource);
}