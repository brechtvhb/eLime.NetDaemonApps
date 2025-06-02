using eLime.NetDaemonApps.Domain.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

internal class EnergyConsumer2StateChangedEvent : EventArgs
{
    public EnergyConsumer2StateChangedEvent(EnergyConsumer2 consumer, EnergyConsumerState state)
    {
        Consumer = consumer;
        State = state;
    }

    public EnergyConsumer2 Consumer { get; set; }
    public EnergyConsumerState State { get; set; }

}

internal class EnergyConsumer2StartCommand : EnergyConsumer2StateChangedEvent
{
    public EnergyConsumer2StartCommand(EnergyConsumer2 consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

internal class EnergyConsumer2StartedEvent : EnergyConsumer2StateChangedEvent
{
    public EnergyConsumer2StartedEvent(EnergyConsumer2 consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

internal class EnergyConsumer2StopCommand : EnergyConsumer2StateChangedEvent
{
    public EnergyConsumer2StopCommand(EnergyConsumer2 consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

internal class EnergyConsumer2StoppedEvent : EnergyConsumer2StateChangedEvent
{
    public EnergyConsumer2StoppedEvent(EnergyConsumer2 consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}