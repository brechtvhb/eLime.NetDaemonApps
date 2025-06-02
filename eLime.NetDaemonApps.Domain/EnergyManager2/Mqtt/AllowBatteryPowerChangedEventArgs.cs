using eLime.NetDaemonApps.Domain.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;

public class AllowBatteryPowerChangedEventArgs : EventArgs
{
    public required AllowBatteryPower AllowBatteryPower;

    public static AllowBatteryPowerChangedEventArgs Create(AllowBatteryPower allowBatteryPower) => new() { AllowBatteryPower = allowBatteryPower };
}