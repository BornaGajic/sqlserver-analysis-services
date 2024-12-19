using SqlServerAnalysisServices.Model;

namespace SqlServerAnalysisServices.Common;

public interface ISsasDatabaseStructure
{
    SsasDatabaseDescription Description { get; }

    SsasDatabase Properties();
}