﻿using eLime.NetDaemonApps.Domain.Scripts;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class WakeUpPcAction : Action
{
    public Script Script { get; init; }
    public string MacAddress { get; init; }

    public WakeUpPcAction(Script script, String macAddress)
    {
        Script = script;

        MacAddress = macAddress ?? throw new ArgumentNullException("scriptData.macAddress");
    }

    public override Task Execute(Boolean isAutoTransition = false)
    {
        Script.WakeUpPc(new WakeUpPcParameters()
        {
            MacAddress = MacAddress
        });

        return Task.CompletedTask;
    }
}