namespace eLime.NetDaemonApps.Domain.EnergyManager;

public enum LoadTimeFrames
{
    Now,
    Last30Seconds,
    LastMinute,
    Last2Minutes,
    Last5Minutes,
    SolarForecastNow,
    SolarForecastNow50PercentCorrected,
    SolarForecast30Minutes,
    SolarForecast1Hour,
}