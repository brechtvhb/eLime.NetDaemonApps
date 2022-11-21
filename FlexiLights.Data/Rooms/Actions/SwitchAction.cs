using FlexiLights.Data.BinarySensors;

namespace FlexiLights.Data.Rooms.Actions;

public class SwitchTurnOnAction : Action
{
    public Switch Switch { get; init; }

    public SwitchTurnOnAction(Switch @switch)
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
    public Switch Switch { get; init; }

    public SwitchTurnOffAction(Switch @switch)
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
    public Switch Switch { get; init; }
    public TimeSpan Duration { get; init; }

    public SwitchPulseAction(Switch @switch, TimeSpan? duration)
    {
        Switch = @switch;
        Duration = duration ?? TimeSpan.FromMilliseconds(200);
    }
    public override async Task Execute(Boolean isAutoTransition = false)
    {
        await Switch.Pulse(Duration);
    }
}