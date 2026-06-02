using Dapper;

namespace SqlServerAnalysisServices.Utility;

internal static class SqlMapperUtility
{
    public static void TryAddSqlTypeHandlers()
    {
        if (!SqlMapper.HasTypeHandler(typeof(XmlTypeHandler)))
            SqlMapper.AddTypeHandler(new XmlTypeHandler());

        if (!SqlMapper.HasTypeHandler(typeof(DapperDateOnlyTypeHandler)))
            SqlMapper.AddTypeHandler(new DapperDateOnlyTypeHandler());

        if (!SqlMapper.HasTypeHandler(typeof(DapperTimeOnlyTypeHandler)))
            SqlMapper.AddTypeHandler(new DapperTimeOnlyTypeHandler());
    }
}