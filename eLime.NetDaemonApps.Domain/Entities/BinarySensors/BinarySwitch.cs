using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record BinarySwitch : BinarySensor, ISwitch, IDisposable
{
    private IDisposable _subscribeDisposable;

    private DateTime? _clickStartDateTime;
    private int _clickCount;
    private DebounceDispatcher ClickDebounceDispatcher;

    public TimeSpan ClickInterval { get; private set; }
    public TimeSpan LongClickDuration { get; private set; }
    public TimeSpan UberLongClickDuration { get; private set; }
    public BinarySwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public BinarySwitch(Entity entity) : base(entity)
    {
    }

    public void Initialize(TimeSpan? clickInterval, TimeSpan? longClickDuration, TimeSpan? uberLongClickDuration)
    {
        ClickInterval = clickInterval ?? TimeSpan.FromMilliseconds(350);
        LongClickDuration = longClickDuration ?? TimeSpan.FromSeconds(1);
        UberLongClickDuration = uberLongClickDuration ?? TimeSpan.FromSeconds(3);
        ClickDebounceDispatcher = new DebounceDispatcher(ClickInterval);

        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null && x.New.IsOn())
                {
                    OnTurnedOn(new BinarySensorEventArgs(x));
                }
                if (x.New != null && x.New.IsOff())
                {
                    OnTurnedOff(new BinarySensorEventArgs(x));
                }
            });
    }

    public static BinarySwitch Create(IHaContext haContext, string entityId)
    {
        var @switch = new BinarySwitch(haContext, entityId);
        @switch.Initialize();
        return @switch;
    }


    public static BinarySwitch Create(IHaContext haContext, string entityId, TimeSpan? clickInterval, TimeSpan? longClickDuration, TimeSpan? uberLongClickDuration)
    {
        var @switch = new BinarySwitch(haContext, entityId);
        @switch.Initialize(clickInterval, longClickDuration, uberLongClickDuration);
        return @switch;
    }

    public event EventHandler<SwitchEventArgs>? Clicked;
    public event EventHandler<SwitchEventArgs>? DoubleClicked;
    public event EventHandler<SwitchEventArgs>? TripleClicked;
    public event EventHandler<SwitchEventArgs>? LongClicked;
    public event EventHandler<SwitchEventArgs>? UberLongClicked;

    private new void OnTurnedOn(BinarySensorEventArgs e)
    {
        _clickStartDateTime = DateTime.Now;
    }
    private new void OnTurnedOff(BinarySensorEventArgs e)
    {
        if (_clickStartDateTime == null)
            return; //can happen if you create a virtual switch which transitions from state unknown to off when it is created

        var clickDuration = DateTime.Now - _clickStartDateTime;

        if (clickDuration > UberLongClickDuration)
        {
            UberLongClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
            _clickCount = 0;
            return;
        }

        if (clickDuration > LongClickDuration)
        {
            LongClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
            _clickCount = 0;
            return;
        }

        _clickCount++;
        ClickDebounceDispatcher.Debounce(() => FireAppropriateClickEvent(e));

    }

    private void FireAppropriateClickEvent(BinarySensorEventArgs e)
    {
        switch (_clickCount)
        {
            case 1:
                Clicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
            case 2:
                DoubleClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                _clickCount = 0;
                break;
            default:
                TripleClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                _clickCount = 0;
                break;
        }
        _clickCount = 0;
    }
    public new void Dispose()
    {
        base.Dispose();
        _subscribeDisposable?.Dispose();
    }

    //TODO: enhance?
    ///<summary>Turn a switch on and off</summary>
    public async Task Pulse(TimeSpan pulseDuration)
    {
        CallService("turn_on");
        await Task.Delay(pulseDuration);
        CallService("turn_off");
    }
    ///<summary>Toggle a switch</summary>
    public void Toggle()
    {
        CallService("toggle");
    }
    ///<summary>Turn a switch on</summary>
    public void TurnOn()
    {
        CallService("turn_on");
    }

    ///<summary>Turn a switch off</summary>
    public void TurnOff()
    {
        CallService("turn_off");
    }


}