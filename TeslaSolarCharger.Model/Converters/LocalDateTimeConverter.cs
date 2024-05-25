using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TeslaSolarCharger.Model.Converters;

public class LocalDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public LocalDateTimeConverter()
        : base(
            v => v.ToUniversalTime(), // Store as UTC
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc).ToLocalTime()) // Convert to Local on read
    {
    }
}
