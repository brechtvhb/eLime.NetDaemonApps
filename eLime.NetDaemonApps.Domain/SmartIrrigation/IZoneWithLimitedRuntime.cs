using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public interface IZoneWithLimitedRuntime
{
    TimeSpan? GetRunTime(ILogger logger, DateTimeOffset now);
}