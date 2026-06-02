using Dapper;
using System.Data;

namespace SqlServerAnalysisServices.Utility;

public class DapperTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value)
        => TimeOnly.FromTimeSpan((TimeSpan)value);

    public override void SetValue(IDbDataParameter parameter, TimeOnly time)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = time.ToString();
    }
}