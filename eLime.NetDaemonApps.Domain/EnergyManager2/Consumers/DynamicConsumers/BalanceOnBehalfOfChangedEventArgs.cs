namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.DynamicConsumers;

public class BalanceOnBehalfOfChangedEventArgs : EventArgs
{
    public required string BalanceOnBehalfOf;

    public static BalanceOnBehalfOfChangedEventArgs Create(string balanceOnBehalfOf) => new() { BalanceOnBehalfOf = balanceOnBehalfOf };
}