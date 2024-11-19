using Microsoft.AnalysisServices.AdomdClient;

namespace Framework.Common;

public interface ISsasConnectionFactory
{
    AdomdConnection Create();
}