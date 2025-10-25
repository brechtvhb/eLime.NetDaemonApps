namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public interface ISmartHeatPumpHttpClient : IDisposable
{
    Task<bool> SetMaximumHotWaterTemperature(decimal temperature);
    Task<decimal?> GetMaximumHotWaterTemperature();
}
