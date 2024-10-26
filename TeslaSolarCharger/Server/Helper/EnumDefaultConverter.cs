using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TeslaSolarCharger.Server.Helper;

public class EnumDefaultConverter<T>(T defaultValue) : StringEnumConverter
    where T : struct, Enum
{
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value == null)
        {
            return defaultValue;
        }

        var valueString = reader.Value.ToString();

        // Handle integer-based enums
        if (int.TryParse(valueString, out var intValue))
        {
            if (Enum.IsDefined(typeof(T), intValue))
            {
                return (T)(object)intValue;
            }
        }
        // Handle string-based enums
        else if (Enum.TryParse(valueString, ignoreCase: true, out T parsedEnum))
        {
            return parsedEnum;
        }

        // Return the default value if the input doesn't match any known enum values
        return defaultValue;
    }
}
