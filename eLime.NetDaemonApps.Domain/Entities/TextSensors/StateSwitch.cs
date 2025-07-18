﻿using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.TextSensors;


public record StateSwitch : TextSensor, ISwitch, IDisposable
{
    private IDisposable _subscribeDisposable;

    public string SinglePressState { get; private set; }
    public string DoublePressState { get; private set; }
    public string TriplePressState { get; private set; }
    public string LongPressState { get; private set; }
    public string UberLongPressState { get; private set; }
    public StateSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public StateSwitch(Entity entity) : base(entity)
    {
    }

    public void Initialize(string? singlePressState, string? doublePressState, string? triplePressState, string? longPressState, string? uberLongPressState)
    {
        SinglePressState = singlePressState ?? "single-press";
        DoublePressState = doublePressState ?? "double-press";
        TriplePressState = triplePressState ?? "triple-press";
        LongPressState = longPressState ?? "long-press";
        UberLongPressState = uberLongPressState ?? "uber-long-press";

        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null)
                {
                    OnStateChanged(new TextSensorEventArgs(x));
                }
            });
    }

    public static StateSwitch Create(IHaContext haContext, string entityId, string? singlePressState, string? doublePressState, string? triplePressState, string? longPressState, string? uberLongPressState)
    {
        var @switch = new StateSwitch(haContext, entityId);
        @switch.Initialize(singlePressState, doublePressState, triplePressState, longPressState, uberLongPressState);
        return @switch;
    }

    public event EventHandler<SwitchEventArgs>? Clicked;
    public event EventHandler<SwitchEventArgs>? DoubleClicked;
    public event EventHandler<SwitchEventArgs>? TripleClicked;
    public event EventHandler<SwitchEventArgs>? LongClicked;
    public event EventHandler<SwitchEventArgs>? UberLongClicked;

    private void OnStateChanged(TextSensorEventArgs e)
    {
        switch (e.New.State)
        {
            case { } when e.New.State == SinglePressState:
                Clicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
            case { } when e.New.State == DoublePressState:
                DoubleClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
            case { } when e.New.State == TriplePressState:
                TripleClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
            case { } when e.New.State == LongPressState:
                LongClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
            case { } when e.New.State == UberLongPressState:
                UberLongClicked?.Invoke(this, new SwitchEventArgs(e.Sensor));
                break;
        }
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}