using System.Text.Json.Serialization;
using System.Text.Json;

namespace SqlServerAnalysisServices.Utility;

public static class CustomJsonSerializerOptions
{
    public static JsonSerializerOptions Default => ApplyJsonOptions(new JsonSerializerOptions());

    public static JsonSerializerOptions ApplyJsonOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(new JsonPascalCaseNamingPolicy()));
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.AllowTrailingCommas = true;

        return options;
    }
}