using System.Xml;

namespace SqlServerAnalysisServices.Extensions;

public class XmlaParsing
{
    public static string ApplyEffectiveUserName(string request, string userName)
    {
        var document = new XmlDocument();
        document.LoadXml(request);

        var propertyListNode = document.GetElementsByTagName("PropertyList")
            .OfType<XmlElement>()
            .FirstOrDefault();

        if (propertyListNode != null)
        {
            var effectiveUserNameNode = document.CreateElement("EffectiveUserName", propertyListNode.NamespaceURI);
            effectiveUserNameNode.InnerText = userName;

            propertyListNode.AppendChild(effectiveUserNameNode);

            return document.OuterXml;
        }

        return request;
    }

    public static bool IsDrillthroughRequest(string request) => request.Contains("DRILLTHROUGH");

    public static string ProcessDrillthroughRequest(string request) => IsDrillthroughRequest(request)
        ? request.Replace("<Content>Data</Content>", "<Content>SchemaData</Content>")
        : request;

    public static string ProcessDrillthroughResponse(string response)
    {
        var document = new XmlDocument();
        document.LoadXml(response);

        var headerNodes = document.GetElementsByTagName("xsd:element")
            .OfType<XmlElement>()
            .Where(x => x.HasAttribute("sql:field"))
            .ToList();

        var dataRows = document.GetElementsByTagName("row").OfType<XmlElement>().ToList();

        if (dataRows.Count == 0 || dataRows.Max(dr => dr.ChildNodes.Count) == headerNodes.Count || headerNodes.Count == 0)
            return response;

        foreach (var row in dataRows)
        {
            var columns = row.ChildNodes.OfType<XmlElement>()
                .Select(c => new
                {
                    Name = XmlConvert.DecodeName(c.Name),
                    Value = c
                }).ToList();
            row.RemoveAll();

            for (var i = 0; i < headerNodes.Count; i++)
            {
                var header = headerNodes[i];
                var headerName = header.GetAttribute("sql:field");
                var column = columns.FirstOrDefault(c => XmlConvert.DecodeName(c.Name) == headerName)?.Value;
                column ??= document.CreateElement(XmlConvert.EncodeName(headerName));

                row.AppendChild(column);
            }
        }

        return document.OuterXml;
    }
}