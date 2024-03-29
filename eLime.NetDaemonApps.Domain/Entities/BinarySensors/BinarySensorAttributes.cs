﻿using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record BinarySensorAttributes
{

    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; init; }

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }
}