using Dapper;
using System.Data;
using System.Xml.Linq;

namespace SqlServerAnalysisServices.Utility;

public class XmlTypeHandler : SqlMapper.TypeHandler<XDocument>
{
    public override XDocument Parse(object value)
    {
        return XDocument.Parse(value.ToString());
    }

    public override void SetValue(IDbDataParameter parameter, XDocument value)
    {
        throw new NotImplementedException();
    }
}