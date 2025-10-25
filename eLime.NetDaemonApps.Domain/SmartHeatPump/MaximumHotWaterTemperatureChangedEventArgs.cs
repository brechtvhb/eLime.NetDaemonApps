namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class MaximumHotWaterTemperatureChangedEventArgs : EventArgs
{
    public required decimal Temperature;

    public static MaximumHotWaterTemperatureChangedEventArgs Create(double temperature) => new() { Temperature = Convert.ToDecimal(temperature) };
}
