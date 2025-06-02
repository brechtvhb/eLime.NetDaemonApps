namespace eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;

public class BalanceOnBehalfOfChangedEventArgs : EventArgs
{
    public required string BalanceOnBehalfOf;

    public static BalanceOnBehalfOfChangedEventArgs Create(string balanceOnBehalfOf) => new() { BalanceOnBehalfOf = balanceOnBehalfOf };
}