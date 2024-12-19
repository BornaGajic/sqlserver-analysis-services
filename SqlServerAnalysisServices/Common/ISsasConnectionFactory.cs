using Microsoft.AnalysisServices.AdomdClient;

namespace SqlServerAnalysisServices.Common;

public interface ISsasConnectionFactory
{
    AdomdConnection Create();
}