﻿using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Mqtt;

public class NumericSensorOptions : EntityOptions
{
    [JsonPropertyName("unit_of_measurement")]
    public String UnitOfMeasurement { get; set; }
}