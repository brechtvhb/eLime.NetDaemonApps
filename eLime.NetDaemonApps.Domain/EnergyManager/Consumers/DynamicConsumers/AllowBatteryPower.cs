namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

public enum AllowBatteryPower
{
    Yes, //Remove
    //MaxPower, (GridMonitor.LoadMinusBatteries + BatteryManager.MaxChargePower (keep empty batteries in mind) / Batteries allowed to discharge)
    //FlattenGridLoad, (GridMonitor.LoadMinusBatteries / Batteries allowed to discharge)
    No //(GridMonitor.LoadMinusBatteries / Batteries NOT allowed to discharge)
}