using System.Dynamic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SqlServerAnalysisServices.Utility;

public enum FloatFormat
{
    Double,
    Decimal,
}

public enum ObjectFormat
{
    Expando,
    Dictionary,
}

public enum UnknownNumberFormat
{
    Error,
    JsonElement,
}

public class ObjectAsPrimitiveConverter : JsonConverter<object>
{
    public ObjectAsPrimitiveConverter() : this(FloatFormat.Decimal, UnknownNumberFormat.Error, ObjectFormat.Expando)
    {
    }

    public ObjectAsPrimitiveConverter(FloatFormat floatFormat, UnknownNumberFormat unknownNumberFormat, ObjectFormat objectFormat)
    {
        FloatFormat = floatFormat;
        UnknownNumberFormat = unknownNumberFormat;
        ObjectFormat = objectFormat;
    }

    FloatFormat FloatFormat { get; init; }
    ObjectFormat ObjectFormat { get; init; }
    UnknownNumberFormat UnknownNumberFormat { get; init; }

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.True:
                return true;
            // If you want to, you could add a heuristic that automatically recognizes and returns DateTime values here:
            //case JsonTokenType.String when reader.TryGetDateTime(out var dt):
            //  return dt;
            // Or if you would prefer not to lose time zone info, you could add automatic DateTimeOffset recognition here:
            //case JsonTokenType.String when reader.TryGetDateTimeOffset(out var dt):
            //  return dt;
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
            {
                if (reader.TryGetInt32(out var i))
                    return i;
                if (reader.TryGetInt64(out var l))
                    return l;
                // BigInteger could be added here.
                if (FloatFormat == FloatFormat.Decimal && reader.TryGetDecimal(out var m))
                    return m;
                else if (FloatFormat == FloatFormat.Double && reader.TryGetDouble(out var d))
                    return d;
                using var doc = JsonDocument.ParseValue(ref reader);
                if (UnknownNumberFormat == UnknownNumberFormat.JsonElement)
                    return doc.RootElement.Clone();
                throw new JsonException(string.Format("Cannot parse number {0}", doc.RootElement.ToString()));
            }
            case JsonTokenType.StartArray:
            {
                var list = new List<object>();
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        default:
                            list.Add(Read(ref reader, typeof(object), options));
                            break;

                        case JsonTokenType.EndArray:
                            return list;
                    }
                }
                throw new JsonException();
            }
            case JsonTokenType.StartObject:
                var dict = CreateDictionary();
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.EndObject:
                            return dict;

                        case JsonTokenType.PropertyName:
                            var key = reader.GetString()!;
                            reader.Read();
                            dict.Add(key, Read(ref reader, typeof(object), options));
                            break;

                        default:
                            throw new JsonException();
                    }
                }
                throw new JsonException();
            default:
                throw new JsonException(string.Format("Unknown token {0}", reader.TokenType));
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else if (value.GetType() == typeof(object))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }

    protected virtual IDictionary<string, object> CreateDictionary() => ObjectFormat == ObjectFormat.Expando ? new ExpandoObject() : new Dictionary<string, object>();
}