using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Text.Json.Serialization;

namespace eLime.NetDaemonApps.Domain.Entities.ClimateEntities;

public record Climate : Entity<Climate, EntityState<ClimateAttributes>, ClimateAttributes>, IDisposable
{
    private IDisposable _subscribeDisposable;

    public Climate(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
        Initialize();
    }

    public Climate(Entity entity) : base(entity)
    {
        Initialize();
    }

    public void Initialize()
    {
        _subscribeDisposable = StateAllChanges()
            .Subscribe(x =>
            {
                if (x.New == null)
                    return;

                if (x.Old?.Attributes?.FanMode == x.New?.Attributes?.FanMode)
                    return;

                OnStateChanged(new ClimateEventArgs(x));
            });
    }

    public event EventHandler<ClimateEventArgs>? StateChanged;
    protected void OnStateChanged(ClimateEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }


    public void SetFanMode(ClimateSetFanModeParameters data)
    {
        CallService("set_fan_mode", data);
    }


    public void SetFanMode(string fanMode)
    {
        CallService("set_fan_mode", new ClimateSetFanModeParameters { FanMode = fanMode });
    }

    public void SetFanMode(VentilationState fanMode)
    {
        CallService("set_fan_mode", new ClimateSetFanModeParameters { FanMode = fanMode.ToString().ToLower() });
    }


    public void SetTemperature(ClimateSetTemperatureParameters data)
    {
        CallService("set_temperature", data);
    }

    public void SetTemperature(Double targetTemperature)
    {
        CallService("set_temperature", new ClimateSetTemperatureParameters { Temperature = targetTemperature });
    }

    public VentilationState? Mode => String.IsNullOrWhiteSpace(Attributes?.FanMode) ? null : Enum<VentilationState>.Cast(Attributes.FanMode);

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}

public record ClimateSetFanModeParameters
{
    ///<summary>New value of fan mode. eg: low</summary>
    [JsonPropertyName("fan_mode")]
    public string? FanMode { get; init; }
}

public record ClimateSetTemperatureParameters
{
    ///<summary>New target temperature for HVAC.</summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
}