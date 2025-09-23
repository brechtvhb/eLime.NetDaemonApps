namespace eLime.NetDaemonApps.Domain.EnergyManager;

public enum LoadTimeFrames
{
    Now,
    Last30Seconds,
    LastMinute,
    Last2Minutes,
    Last5Minutes,
    SolarForecastNowCorrected,
    SolarForecastNow50PercentCorrected,
    SolarForecast30MinutesCorrected,
    SolarForecast1HourCorrected,
}