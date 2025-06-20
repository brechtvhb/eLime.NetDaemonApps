﻿namespace eLime.NetDaemonApps.Domain.EnergyManager;

internal class EnergyConsumerFileStorage
{
    public EnergyConsumerState State { get; set; }
    public BalancingMethod? BalancingMethod { get; set; }
    public string? BalanceOnBehalfOf { get; set; }
    public AllowBatteryPower? AllowBatteryPower { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastRun { get; set; }

}