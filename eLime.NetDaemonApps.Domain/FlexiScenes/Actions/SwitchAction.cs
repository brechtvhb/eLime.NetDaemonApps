using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class SwitchTurnOnAction : Action
{
    public BinarySwitch Switch { get; init; }

    public SwitchTurnOnAction(BinarySwitch @switch)
    {
        Switch = @switch;
    }
    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        bool? stateChanged = null;
        if (detectStateChange)
            stateChanged = Switch.IsOff();

        Switch.TurnOn();

        return Task.FromResult(stateChanged);
    }

    public override Action Reverse()
    {
        return new SwitchTurnOffAction(Switch);
    }
}

public class SwitchTurnOffAction : Action
{
    public BinarySwitch Switch { get; init; }

    public SwitchTurnOffAction(BinarySwitch @switch)
    {
        Switch = @switch;
    }
    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        bool? initialState = null;
        if (detectStateChange)
            initialState = Switch.IsOn();

        Switch.TurnOff();

        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return new SwitchTurnOnAction(Switch);
    }
}

public class SwitchToggleAction : Action
{
    public BinarySwitch Switch { get; init; }

    public SwitchToggleAction(BinarySwitch @switch)
    {
        Switch = @switch;
    }

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        bool? stateChanged = null;
        if (detectStateChange)
            stateChanged = Switch.IsOn() || Switch.IsOff();

        Switch.Toggle();
        return Task.FromResult(stateChanged);
    }

    public override Action Reverse()
    {
        return new SwitchToggleAction(Switch);
    }
}

public class SwitchPulseAction : Action
{
    public BinarySwitch Switch { get; init; }
    public TimeSpan Duration { get; init; }

    public SwitchPulseAction(BinarySwitch @switch, TimeSpan? duration)
    {
        Switch = @switch;
        Duration = duration ?? TimeSpan.FromMilliseconds(200);
    }
    public override async Task<bool?> Execute(bool detectStateChange = false)
    {
        await Switch.Pulse(Duration);

        return null;
    }

    public override Action Reverse()
    {
        return null;
    }
}