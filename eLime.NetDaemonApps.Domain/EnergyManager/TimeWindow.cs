using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class TimeWindow
{
    public BinarySensor? Active { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeWindow(BinarySensor? active, TimeOnly start, TimeOnly end)
    {
        Active = active;
        Start = start;
        End = end;
    }

    public bool IsActive(DateTimeOffset now)
    {
        if (Active != null && Active.IsOff())
            return false;

        var start = now.Add(-now.TimeOfDay).Add(Start.ToTimeSpan());
        var end = now.Add(-now.TimeOfDay).Add(End.ToTimeSpan());

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