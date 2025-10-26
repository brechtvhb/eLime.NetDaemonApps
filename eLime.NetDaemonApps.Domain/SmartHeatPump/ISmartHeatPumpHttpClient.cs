namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public interface ISmartHeatPumpHttpClient : IDisposable
{
    Task<decimal?> GetMaximumHotWaterTemperature();
    Task<bool> SetMaximumHotWaterTemperature(decimal temperature);

    Task<decimal?> GetEcoRoomTemperature();
    Task<bool> SetEcoRoomTemperature(decimal temperature);
}
