namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public interface IZoneWithLimitedRuntime
{
    TimeSpan? GetRunTime(DateTimeOffset now);
}