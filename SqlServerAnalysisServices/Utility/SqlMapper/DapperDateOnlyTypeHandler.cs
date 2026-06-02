using Dapper;
using System.Data;

namespace SqlServerAnalysisServices.Utility;

public class DapperDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value)
        => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly date)
    {
        parameter.DbType = DbType.DateTime;
        parameter.Value = date.ToDateTime(new TimeOnly(0, 0));
    }
}