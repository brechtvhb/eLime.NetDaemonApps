﻿using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Rooms.Evaluations.Abstractions;

public interface IEvaluation
{
    public bool Evaluate(IReadOnlyCollection<Entity> sensors);

    public IReadOnlyCollection<(string, EvaluationSensorType)> GetSensorsIds();
}