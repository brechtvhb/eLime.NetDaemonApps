using eLime.NetDaemonApps.Domain.BinarySensors;

namespace eLime.NetDaemonApps.Domain.Rooms.Actions;

public class SwitchTurnOnAction : Action
{
    public BinarySwitch Switch { get; init; }

    public SwitchTurnOnAction(BinarySwitch @switch)
    {
        Switch = @switch;
    }
    public override Task Execute(Boolean isAutoTransition = false)
    {
        Switch.TurnOn();
        return Task.CompletedTask;
    }
}

public class SwitchTurnOffAction : Action
{
    public BinarySwitch Switch { get; init; }

    public SwitchTurnOffAction(BinarySwitch @switch)
    {
        Switch = @switch;
    }
    public override Task Execute(Boolean isAutoTransition = false)
    {
        Switch.TurnOff();
        return Task.CompletedTask;
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
    public override async Task Execute(Boolean isAutoTransition = false)
    {
        await Switch.Pulse(Duration);
    }
}