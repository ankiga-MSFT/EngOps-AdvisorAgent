using System.Text.Json;
using System.Text.Json.Serialization;

namespace CXOAI.Memory;

/// <summary>
/// JSON converter factory that creates case-insensitive enum converters.
/// Handles any casing from LLM responses or files: "Permanent", "permanent", "PERMANENT" all work.
/// Writes enum values as PascalCase (default <see cref="Enum.ToString"/>).
/// </summary>
public class CaseInsensitiveEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(CaseInsensitiveEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                    throw new JsonException($"Empty or null string is not a valid {typeof(T)} value.");

                if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
                    return result;

                throw new JsonException($"Unable to convert \"{value}\" to {typeof(T)}.");
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var intValue))
                return (T)Enum.ToObject(typeof(T), intValue);

            throw new JsonException($"Unexpected token {reader.TokenType} when parsing {typeof(T)}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
