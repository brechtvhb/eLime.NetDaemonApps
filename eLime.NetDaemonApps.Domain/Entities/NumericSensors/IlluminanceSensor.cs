﻿using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;


public record IlluminanceSensor : NumericThresholdSensor
{
    public IlluminanceSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public IlluminanceSensor(Entity entity) : base(entity)
    {
    }

    public new static IlluminanceSensor Create(IHaContext haContext, string entityId, double? threshold, TimeSpan thresholdTimeSpan, IScheduler scheduler)
    {
        var sensor = new IlluminanceSensor(haContext, entityId);
        sensor.Initialize(threshold, thresholdTimeSpan, scheduler);
        return sensor;
    }
}