namespace eLime.NetDaemonApps.Domain;

public interface ISwitch : IDisposable
{
    public String EntityId { get; }
    event EventHandler<SwitchEventArgs>? Clicked;
    event EventHandler<SwitchEventArgs>? DoubleClicked;
    event EventHandler<SwitchEventArgs>? TripleClicked;
    event EventHandler<SwitchEventArgs>? LongClicked;
    event EventHandler<SwitchEventArgs>? UberLongClicked;
}