using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class TimeWindow(BinarySensor? active, bool inverted, List<DayOfWeek> days, TimeOnly start, TimeOnly end)
{
    public BinarySensor? Active { get; } = active;
    public bool ActiveInverted { get; } = inverted;
    public List<DayOfWeek> Days { get; } = days;
    public TimeOnly Start { get; } = start;
    public TimeOnly End { get; } = end;

    public bool IsActive(DateTimeOffset now, string timezone)
    {
        if (Active != null && Active.IsOff() && !ActiveInverted)
            return false;

        if (Active != null && Active.IsOn() && ActiveInverted)
            return false;

        if (Days.Count > 0 && !Days.Contains(now.DayOfWeek))
            return false;

        var start = Start.GetUtcDateTimeFromLocalTimeOnly(now.DateTime, timezone);
        var end = End.GetUtcDateTimeFromLocalTimeOnly(now.DateTime, timezone);

        if (Start > End)
        {
            if (now.TimeOfDay > Start.ToTimeSpan())
                end = end.AddDays(1);

            if (now.TimeOfDay < End.ToTimeSpan())
                start = start.AddDays(1);
        }

        return start <= now && end >= now;
    }

    public override string ToString()
    {
        return $"Active: {Active?.EntityId} - {Active?.State}. Between: {Start.ToString("HH:mm")} - {End.ToString("HH:mm")}";
    }
}