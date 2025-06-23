namespace eLime.NetDaemonApps.Domain;

public interface ISwitch : IDisposable
{
    public string EntityId { get; }
    event EventHandler<SwitchEventArgs>? Clicked;
    event EventHandler<SwitchEventArgs>? DoubleClicked;
    event EventHandler<SwitchEventArgs>? TripleClicked;
    event EventHandler<SwitchEventArgs>? LongClicked;
    event EventHandler<SwitchEventArgs>? UberLongClicked;
}