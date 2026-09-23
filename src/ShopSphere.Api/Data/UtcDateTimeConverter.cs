using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ShopSphere.Api.Data;

// SQL Server datetime2 has no time zone, so values come back as Unspecified.
// Everything is stored in UTC, so reads are tagged as UTC and serialize with
// a "Z" that browsers convert to local time correctly.
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
