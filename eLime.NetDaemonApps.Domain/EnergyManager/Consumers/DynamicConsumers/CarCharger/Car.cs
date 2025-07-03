using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;

internal class Car : IDisposable
{
    protected EnergyManagerContext Context { get; }
    internal CarHomeAssistantEntities HomeAssistant { get; }

    public string Name { get; set; }
    public CarChargingMode Mode { get; set; }
    public int? MinimumCurrent { get; set; }
    public int? MaximumCurrent { get; set; }
    public double BatteryCapacity { get; set; }
    public bool RemainOnAtFullBattery { get; set; }
    public bool AutoPowerOnWhenConnecting { get; set; }
    public DateTimeOffset? LastCurrentChange { get; private set; }

    public bool IsConnectedToHomeCharger => HomeAssistant.CableConnectedSensor.IsOn() && HomeAssistant.Location.State == "home";
    public bool CanSetCurrent => IsConnectedToHomeCharger && HomeAssistant.CurrentNumber != null;
    public bool NeedsEnergy => RemainOnAtFullBattery ||
                               (
                                   HomeAssistant.MaxBatteryPercentageSensor != null
                                           ? HomeAssistant.BatteryPercentageSensor.State < HomeAssistant.MaxBatteryPercentageSensor.State
                                           : HomeAssistant.BatteryPercentageSensor.State < 100
                               );

    public bool IsRunning => HomeAssistant.ChargerSwitch == null
            ? !CanSetCurrent || HomeAssistant.CurrentNumber!.State >= MinimumCurrent
            : CanSetCurrent
                ? HomeAssistant.ChargerSwitch.IsOn() && HomeAssistant.CurrentNumber!.State >= MinimumCurrent
                : HomeAssistant.ChargerSwitch.IsOn();


    internal Car(EnergyManagerContext context, CarConfiguration config)
    {
        if (config == null)
            throw new ArgumentException("car configuration is required for Car2.");

        Context = context;
        HomeAssistant = new CarHomeAssistantEntities(config);
        if (HomeAssistant.ChargerSwitch != null)
        {
            HomeAssistant.ChargerSwitch.TurnedOn += ChargerSwitch_TurnedOn;
            HomeAssistant.ChargerSwitch.TurnedOff += ChargerSwitch_TurnedOff;
        }
        HomeAssistant.CableConnectedSensor.TurnedOn += CableConnectedSensor_TurnedOn;

        Name = config.Name;
        Mode = config.Mode;
        MinimumCurrent = config.MinimumCurrent;
        MaximumCurrent = config.MaximumCurrent;
        BatteryCapacity = config.BatteryCapacity;
        RemainOnAtFullBattery = config.RemainOnAtFullBattery;
        AutoPowerOnWhenConnecting = config.AutoPowerOnWhenConnecting;
    }
    public event EventHandler<BinarySensorEventArgs>? ChargerTurnedOn;
    public event EventHandler<BinarySensorEventArgs>? ChargerTurnedOff;
    public event EventHandler<BinarySensorEventArgs>? Connected;
    protected void OnChargerSwitchTurnedOn(BinarySensorEventArgs e)
    {
        ChargerTurnedOn?.Invoke(this, e);
    }
    protected void OnChargerSwitchTurnedOff(BinarySensorEventArgs e)
    {
        ChargerTurnedOff?.Invoke(this, e);
    }
    protected void OnConnected(BinarySensorEventArgs e)
    {
        Connected?.Invoke(this, e);
    }

    private void ChargerSwitch_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        OnChargerSwitchTurnedOn(e);
    }

    private void ChargerSwitch_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        OnChargerSwitchTurnedOff(e);
    }
    private void CableConnectedSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        //Should check for state changes on location too, but one is always home before being able to connect the cable ?
        if (HomeAssistant.Location.State == "home")
            OnConnected(e);
    }

    public void TurnOnCharger()
    {
        HomeAssistant.ChargerSwitch?.TurnOn();
    }
    public void TurnOffCharger()
    {
        HomeAssistant.ChargerSwitch?.TurnOff();
    }


    public void ChangeCurrent(double toBeCurrent)
    {
        if (HomeAssistant.CurrentNumber == null)
            return;

        if (LastCurrentChange?.Add(TimeSpan.FromSeconds(5)) > Context.Scheduler.Now)
            return;

        HomeAssistant.CurrentNumber.Change(toBeCurrent);
        LastCurrentChange = Context.Scheduler.Now;
    }

    public void Dispose()
    {
        HomeAssistant.Dispose();
    }
}