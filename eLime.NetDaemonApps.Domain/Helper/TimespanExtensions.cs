namespace eLime.NetDaemonApps.Domain.Helper;

public static class TimespanExtensions
{
    public static TimeSpan Round(this TimeSpan? input, int precision = 0)
    {
        if (!input.HasValue)
        {
            return TimeSpan.Zero;
        }

        const int TIMESPAN_SIZE = 7; // it always has seven digits
        // convert the digitsToShow into a rounding/truncating mask
        var factor = (int)Math.Pow(10, (TIMESPAN_SIZE - precision));

        var roundedTimeSpan = new TimeSpan(((long)Math.Round((1.0 * input.Value.Ticks / factor)) * factor));
        return roundedTimeSpan;
    }

    public static DateTime GetUtcDateTimeFromLocalTimeOnly(this TimeOnly timeOnly, DateTime utcDate, string timezone /*= "Europe/Brussels"*/)
    {
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZoneInfo);

        var modifiedLocalDateTime = localDateTime.Add(-localDateTime.TimeOfDay).Add(timeOnly.ToTimeSpan());
        return TimeZoneInfo.ConvertTimeToUtc(modifiedLocalDateTime, timeZoneInfo);
    }
    public static DateTime GetLocalDateTimeFromUtcDateTime(this DateTime utcDate, string timezone /*= "Europe/Brussels"*/)
    {
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, timeZoneInfo);
    }
}