using System.Text.RegularExpressions;
using System.Xml;

namespace SqlServerAnalysisServices.Extensions;

public partial class XmlaParsing
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

    /// <summary>
    /// Fabric / Power BI XMLA endpoints alias column names in discover-rowset responses to opaque
    /// tokens like <c>C00</c>, <c>C01</c>, etc., keeping the real names only in the <c>sql:field</c>
    /// attribute. AAS and on-prem SSAS don't do this. Clients that read schemas via the <c>name</c>
    /// attribute (e.g. Flexmonster) then render columns as blanks. This rewrites the schema and the
    /// row element tags so the response looks identical to an AAS/on-prem one.
    /// </summary>
    public static string UnaliasFabricColumnNames(string response)
    {
        if (string.IsNullOrEmpty(response) || !AliasInResponseRegex().IsMatch(response))
            return response;

        var document = new XmlDocument();
        document.LoadXml(response);

        var aliasMap = new Dictionary<string, string>(StringComparer.Ordinal);

        var schemaElements = document.GetElementsByTagName("xsd:element")
            .OfType<XmlElement>()
            .Where(e => e.HasAttribute("name") && e.HasAttribute("sql:field"))
            .ToList();

        foreach (var el in schemaElements)
        {
            var alias = el.GetAttribute("name");
            var realName = el.GetAttribute("sql:field");

            if (AliasNameRegex().IsMatch(alias) && !string.IsNullOrEmpty(realName) && alias != realName)
            {
                var encodedName = XmlConvert.EncodeName(realName);
                aliasMap[alias] = encodedName;
                el.SetAttribute("name", encodedName);
            }
        }

        if (aliasMap.Count == 0)
            return response;

        var rows = document.GetElementsByTagName("row").OfType<XmlElement>().ToList();
        foreach (var row in rows)
        {
            foreach (var child in row.ChildNodes.OfType<XmlElement>().ToList())
            {
                if (aliasMap.TryGetValue(child.LocalName, out var realName))
                {
                    var renamed = document.CreateElement(realName, child.NamespaceURI);
                    renamed.InnerXml = child.InnerXml;
                    foreach (XmlAttribute attr in child.Attributes)
                        renamed.SetAttributeNode((XmlAttribute)attr.CloneNode(true));
                    row.ReplaceChild(renamed, child);
                }
            }
        }

        return document.OuterXml;
    }

    [GeneratedRegex(@"name=""C\d+""", RegexOptions.CultureInvariant)]
    private static partial Regex AliasInResponseRegex();

    [GeneratedRegex(@"^C\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasNameRegex();
}