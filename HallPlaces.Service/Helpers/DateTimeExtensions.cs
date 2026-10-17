namespace HallPlaces.Service.Helpers;

public static class DateTimeExtensions
{
    // PostgreSQL (timestamptz) accepts only UTC. A date that arrives from JSON
    // without a "Z" has Kind = Unspecified, so we treat it as UTC explicitly.
    public static DateTime ToUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
