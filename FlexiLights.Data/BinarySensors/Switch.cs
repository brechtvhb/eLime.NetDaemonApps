using FlexiLights.Data.Helper;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.BinarySensors;

//TODO: Logic for single, double, triple and Long click
public record Switch : BinarySensor
{
    private DateTime? _clickStartDateTime;
    private Int32 _clickCount;
    private DebounceDispatcher ClickDebounceDispatcher;

    public TimeSpan ClickInterval { get; private set; }
    public TimeSpan LongClickDuration { get; private set; }
    public TimeSpan UberLongClickDuration { get; private set; }
    public Switch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public Switch(Entity entity) : base(entity)
    {
    }

    public void Initialize(TimeSpan? clickInterval, TimeSpan? longClickDuration, TimeSpan? uberLongClickDuration)
    {
        ClickInterval = clickInterval ?? TimeSpan.FromMilliseconds(400);
        LongClickDuration = longClickDuration ?? TimeSpan.FromSeconds(1);
        UberLongClickDuration = uberLongClickDuration ?? TimeSpan.FromSeconds(3);
        ClickDebounceDispatcher = new DebounceDispatcher(ClickInterval);

        StateChanges()
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

    public static Switch Create(IHaContext haContext, string entityId, TimeSpan? clickInterval, TimeSpan? longClickDuration, TimeSpan? uberLongClickDuration)
    {
        var @switch = new Switch(haContext, entityId);
        @switch.Initialize(clickInterval, longClickDuration, uberLongClickDuration);
        return @switch;
    }

    public event EventHandler<BinarySensorEventArgs>? Clicked;
    public event EventHandler<BinarySensorEventArgs>? DoubleClicked;
    public event EventHandler<BinarySensorEventArgs>? TripleClicked;
    public event EventHandler<BinarySensorEventArgs>? LongClicked;
    public event EventHandler<BinarySensorEventArgs>? UberLongClicked;

    private new void OnTurnedOn(BinarySensorEventArgs e)
    {
        _clickStartDateTime = DateTime.Now;
    }
    private new void OnTurnedOff(BinarySensorEventArgs e)
    {
        if (_clickStartDateTime == null)
            throw new Exception("Technically not possible afaik?");

        var clickDuration = DateTime.Now - _clickStartDateTime;

        if (clickDuration > UberLongClickDuration)
        {
            UberLongClicked?.Invoke(this, e);
            _clickCount = 0;
            return;
        }

        if (clickDuration > LongClickDuration)
        {
            LongClicked?.Invoke(this, e);
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
                Clicked?.Invoke(this, e);
                break;
            case 2:
                DoubleClicked?.Invoke(this, e);
                _clickCount = 0;
                break;
            default:
                TripleClicked?.Invoke(this, e);
                _clickCount = 0;
                break;
        }
        _clickCount = 0;
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