using System.Text.Json.Serialization;
using System.Text.Json;

namespace SqlServerAnalysisServices.Utility;

public static class CustomJsonSerializerOptions
{
    private static readonly Lazy<JsonSerializerOptions> _default = new(() =>
    {
        var options = CreateNew();
        options.MakeReadOnly(true);
        return options;
    });

    private static readonly Lazy<JsonSerializerOptions> _defaultWithIgnoreNull = new(() =>
    {
        var options = CreateNew();
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull | JsonIgnoreCondition.WhenWritingDefault;
        options.MakeReadOnly(true);
        return options;
    });

    public static JsonSerializerOptions Default => _default.Value;
    public static JsonSerializerOptions DefaultWithIgnoreNull => _defaultWithIgnoreNull.Value;

    public static JsonSerializerOptions ApplyJsonOptions(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(new JsonPascalCaseNamingPolicy()));
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new ObjectAsPrimitiveConverter());
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.ReadCommentHandling = JsonCommentHandling.Skip;
        options.NumberHandling = JsonNumberHandling.Strict;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.AllowTrailingCommas = true;
        options.IgnoreReadOnlyFields = true;

        return options;
    }

    public static JsonSerializerOptions CreateNew() => ApplyJsonOptions(new JsonSerializerOptions());
}