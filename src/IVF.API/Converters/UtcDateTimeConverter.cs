using System.Text.Json;
using System.Text.Json.Serialization;

namespace IVF.API.Converters;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var date = reader.GetDateTime();
        return date.Kind == DateTimeKind.Local 
            ? date.ToUniversalTime() 
            : DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime());
    }
}
