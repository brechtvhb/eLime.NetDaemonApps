namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

public class AllowBatteryPowerChangedEventArgs : EventArgs
{
    public required AllowBatteryPower AllowBatteryPower;

    public static AllowBatteryPowerChangedEventArgs Create(AllowBatteryPower allowBatteryPower) => new() { AllowBatteryPower = allowBatteryPower };
}