namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

#pragma warning disable CS8618, CS9264
internal class BatteryManagerState
{
    public decimal TotalAvailableCapacity { get; set; }
    public decimal RemainingAvailableCapacity { get; set; }
    public int StateOfCharge { get; set; }
}