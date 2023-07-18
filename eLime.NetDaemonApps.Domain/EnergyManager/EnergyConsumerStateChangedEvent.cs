namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyConsumerStateChangedEvent : EventArgs
{
    public EnergyConsumerStateChangedEvent(EnergyConsumer consumer, EnergyConsumerState state)
    {
        Consumer = consumer;
        State = state;
    }

    public EnergyConsumer Consumer { get; set; }
    public EnergyConsumerState State { get; set; }

}

public class EnergyConsumerStartCommand : EnergyConsumerStateChangedEvent
{
    public EnergyConsumerStartCommand(EnergyConsumer consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

public class EnergyConsumerStartedEvent : EnergyConsumerStateChangedEvent
{
    public EnergyConsumerStartedEvent(EnergyConsumer consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

public class EnergyConsumerStopCommand : EnergyConsumerStateChangedEvent
{
    public EnergyConsumerStopCommand(EnergyConsumer consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}

public class EnergyConsumerStoppedEvent : EnergyConsumerStateChangedEvent
{
    public EnergyConsumerStoppedEvent(EnergyConsumer consumer, EnergyConsumerState state) : base(consumer, state)
    {
    }
}