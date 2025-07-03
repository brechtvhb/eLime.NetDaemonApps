using eLime.NetDaemonApps.Domain.EnergyManager.Grid;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;

public class CarChargerEnergyConsumer : EnergyConsumer, IDynamicLoadConsumer
{
    internal sealed override DynamicEnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override CarChargerEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

    internal override bool IsRunning => (ConnectedCar?.IsRunning ?? false) && HomeAssistant.StateSensor.State == CarChargerStates.Charging.ToString(); //&& (CurrentEntity.State ?? 0) > OffCurrent;
    internal override double PeakLoad => MinimumCurrentForConnectedCar * TotalVoltage;

    public List<DynamicEnergyConsumerBalancingMethodBasedLoads> DynamicBalancingMethodBasedLoads { get; }

    internal override double SwitchOnLoad => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.SwitchOnLoad ?? _switchOnLoad
        : _switchOnLoad;

    internal override double SwitchOffLoad => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.SwitchOffLoad ?? _switchOffLoad
        : _switchOffLoad;

    private LoadTimeFrames _loadTimeFrameToCheckOnRebalance { get; init; }

    internal LoadTimeFrames LoadTimeFrameToCheckOnRebalance => State.BalancingMethod.HasValue
        ? DynamicBalancingMethodBasedLoads.FirstOrDefault(x => x.BalancingMethods.Contains(State.BalancingMethod.Value))?.LoadTimeFrameToCheckOnRebalance ?? _loadTimeFrameToCheckOnRebalance
        : _loadTimeFrameToCheckOnRebalance;

    public int MinimumCurrent { get; }
    public int MaximumCurrent { get; }
    public int OffCurrent { get; }

    public double ReleasablePowerWhenBalancingOnBehalfOf => CurrentLoad - MinimumCurrentForConnectedCar * TotalVoltage;
    public TimeSpan MinimumRebalancingInterval => TimeSpan.FromSeconds(30); //TODO: config setting
    private DateTimeOffset? _balancingMethodLastChangedAt;

    public BalancingMethod BalancingMethod => State.BalancingMethod ?? BalancingMethod.SolarOnly;
    public string BalanceOnBehalfOf => State.BalanceOnBehalfOf ?? IDynamicLoadConsumer.CONSUMER_GROUP_SELF;
    public AllowBatteryPower AllowBatteryPower => State.AllowBatteryPower ?? AllowBatteryPower.No;

    internal List<Car> Cars { get; set; } = [];
    private Car? ConnectedCar => Cars.FirstOrDefault(x => x.IsConnectedToHomeCharger);

    private int SinglePhaseVoltage => Convert.ToInt32(HomeAssistant.VoltageSensor.State ?? 230);
    private int TotalVoltage => ConnectedCar?.Mode == CarChargingMode.Ac3Phase ? SinglePhaseVoltage * 3 : SinglePhaseVoltage;
    private int MinimumCurrentForConnectedCar => ConnectedCar == null
        ? MinimumCurrent
        : ConnectedCar.MinimumCurrent < MinimumCurrent
            ? ConnectedCar.MinimumCurrent ?? MinimumCurrent
            : MinimumCurrent;
    private int CurrentCurrentForConnectedCar => CurrentLoad > 0 ? Convert.ToInt32(CurrentLoad / TotalVoltage) : 0;
    public DateTimeOffset? _lastCurrentChange;

    internal CarChargerEnergyConsumer(EnergyManagerContext context, EnergyConsumerConfiguration config)
        : base(context, config)
    {
        if (config.CarCharger == null)
            throw new ArgumentException("Simple configuration is required for CarChargerEnergyConsumer.");

        HomeAssistant = new CarChargerEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.CurrentNumber.Changed += CurrentNumber_Changed;
        HomeAssistant.StateSensor.StateChanged += StateSensor_StateChanged;
        MqttSensors = new DynamicEnergyConsumerMqttSensors(config.Name, context);
        MqttSensors.BalancingMethodChanged += BalancingMethodChanged;
        MqttSensors.BalanceOnBehalfOfChanged += BalanceOnBehalfOfChanged;
        MqttSensors.AllowBatteryPowerChanged += AllowBatteryPowerChanged;

        DynamicBalancingMethodBasedLoads = config.DynamicBalancingMethodBasedLoads;
        _loadTimeFrameToCheckOnRebalance = config.LoadTimeFrameToCheckOnRebalance;
        MinimumCurrent = config.CarCharger.MinimumCurrent;
        MaximumCurrent = config.CarCharger.MaximumCurrent;
        OffCurrent = config.CarCharger.OffCurrent;

        foreach (var carConfig in config.CarCharger.Cars)
        {
            var car = new Car(Context, carConfig);
            car.ChargerTurnedOn += Car_ChargerTurnedOn;
            car.ChargerTurnedOff += Car_ChargerTurnedOff;
            car.Connected += Car_Connected;
            Cars.Add(car);
        }

    }
    private void StateSensor_StateChanged(object? sender, Entities.TextSensors.TextSensorEventArgs e)
    {
        if (e.Sensor.State == CarChargerStates.Charging.ToString())
        {
            if (State.State != EnergyConsumerState.Running)
                Started();
        }
    }

    private async void BalancingMethodChanged(object? sender, BalancingMethodChangedEventArgs e)
    {
        try
        {
            State.BalancingMethod = e.BalancingMethod;
            _balancingMethodLastChangedAt = Context.Scheduler.Now;
            Context.Logger.LogInformation("{Name}: Set balancing method to {BalancingMethod}.", Name, e.BalancingMethod);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle BalancingMethodChanged event.");
        }
    }

    private async void BalanceOnBehalfOfChanged(object? sender, BalanceOnBehalfOfChangedEventArgs e)
    {
        try
        {
            State.BalanceOnBehalfOf = e.BalanceOnBehalfOf;
            Context.Logger.LogInformation("{Name}: Set balance on behalf of to {BalanceOnBehalfOf}.", Name, e.BalanceOnBehalfOf);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle BalanceOnBehalfOfChanged event.");
        }
    }

    private async void AllowBatteryPowerChanged(object? sender, AllowBatteryPowerChangedEventArgs e)
    {
        try
        {
            State.AllowBatteryPower = e.AllowBatteryPower;
            Context.Logger.LogInformation("{Name}: Set allow battery power to {AllowBatteryPower}.", Name, e.AllowBatteryPower);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle AllowBatteryPowerChanged event.");
        }
    }

    public (double current, double netPowerChange) Rebalance(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double dynamicLoadAdjustments)
    {
        if (_lastCurrentChange?.Add(MinimumRebalancingInterval) > Context.Scheduler.Now)
            return (0, 0);

        if (HomeAssistant.CurrentNumber.State == null)
            return (0, 0);

        var uncorrectedLoad = LoadTimeFrameToCheckOnRebalance switch
        {
            LoadTimeFrames.Now when AllowBatteryPower is AllowBatteryPower.No or AllowBatteryPower.FlattenGridLoad => gridMonitor.CurrentLoadMinusBatteries,
            LoadTimeFrames.Now when AllowBatteryPower is AllowBatteryPower.MaxPower => gridMonitor.CurrentLoad,
            LoadTimeFrames.Last30Seconds when AllowBatteryPower is AllowBatteryPower.No or AllowBatteryPower.FlattenGridLoad => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromSeconds(30)),
            LoadTimeFrames.Last30Seconds when AllowBatteryPower is AllowBatteryPower.MaxPower => gridMonitor.AverageLoad(TimeSpan.FromSeconds(30)),
            LoadTimeFrames.LastMinute when AllowBatteryPower is AllowBatteryPower.No or AllowBatteryPower.FlattenGridLoad => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(1)),
            LoadTimeFrames.LastMinute when AllowBatteryPower is AllowBatteryPower.MaxPower => gridMonitor.AverageLoad(TimeSpan.FromMinutes(1)),
            LoadTimeFrames.Last2Minutes when AllowBatteryPower is AllowBatteryPower.No or AllowBatteryPower.FlattenGridLoad => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(2)),
            LoadTimeFrames.Last2Minutes when AllowBatteryPower is AllowBatteryPower.MaxPower => gridMonitor.AverageLoad(TimeSpan.FromMinutes(2)),
            LoadTimeFrames.Last5Minutes when AllowBatteryPower is AllowBatteryPower.No or AllowBatteryPower.FlattenGridLoad => gridMonitor.AverageLoadMinusBatteries(TimeSpan.FromMinutes(5)),
            LoadTimeFrames.Last5Minutes when AllowBatteryPower is AllowBatteryPower.MaxPower => gridMonitor.AverageLoad(TimeSpan.FromMinutes(5)),
            _ => throw new ArgumentOutOfRangeException()
        };
        var consumerAverageLoadCorrection = consumerAverageLoadCorrections[LoadTimeFrameToCheckOnRebalance];
        var estimatedLoad = uncorrectedLoad + consumerAverageLoadCorrection + dynamicLoadAdjustments;

        var currentAdjustment = BalancingMethod switch
        {
            _ when HomeAssistant.CriticallyNeededSensor?.IsOn() == true => GetNearPeakAdjustedGridCurrent(estimatedLoad, gridMonitor.PeakLoad),
            BalancingMethod.MaximizeQuarterPeak => GetMaximizeQuarterPeakAdjustedGridCurrent(estimatedLoad, gridMonitor.PeakLoad, gridMonitor.CurrentAverageDemand),
            BalancingMethod.NearPeak => GetNearPeakAdjustedGridCurrent(estimatedLoad, gridMonitor.PeakLoad),
            BalancingMethod.MidPeak => GetMidPeakAdjustedGridCurrent(estimatedLoad, gridMonitor.PeakLoad),
            BalancingMethod.SolarPreferred => GetSolarPreferredAdjustedGridCurrent(estimatedLoad),
            BalancingMethod.MidPoint => GetMidpointAdjustedGridCurrent(estimatedLoad),
            BalancingMethod.SolarSurplus => GetSolarSurplusAdjustedGridCurrent(estimatedLoad),
            _ => GetSolarOnlyAdjustedGridCurrent(estimatedLoad),
        };

        var currentChargerCurrent = HomeAssistant.CurrentNumber.State ?? 0;
        double toBeChargerCurrent;
        double toBeCarCurrent = 0;

        if (ConnectedCar?.CanSetCurrent ?? false)
        {
            toBeCarCurrent = CurrentCurrentForConnectedCar - currentAdjustment;
            toBeChargerCurrent = toBeCarCurrent;
        }
        else
        {
            toBeChargerCurrent = currentChargerCurrent - currentAdjustment;
        }

        var (chargerCurrent, chargerCurrentChanged) = SetChargerCurrent(ConnectedCar, toBeChargerCurrent, currentChargerCurrent);
        var (carCurrent, carCurrentChanged) = SetCarCurrentIfSupported(toBeCarCurrent, CurrentCurrentForConnectedCar);

        if (!chargerCurrentChanged && !carCurrentChanged)
            return (0, 0);

        _lastCurrentChange = Context.Scheduler.Now;

        var netCurrentChange = ConnectedCar?.CanSetCurrent ?? false
            ? carCurrent - CurrentCurrentForConnectedCar
            : chargerCurrent - currentChargerCurrent;

        return (ConnectedCar?.CanSetCurrent ?? false ? carCurrent : chargerCurrent, netCurrentChange * TotalVoltage);
    }

    public double GetMaximizeQuarterPeakAdjustedGridCurrent(double netGridUsage, double peakUsageThisMonth, double currentAverageDemand)
    {
        var availableLoadToReachPeak = peakUsageThisMonth - currentAverageDemand;
        var remainingMinutesThisQuarter = 15 - (Context.Scheduler.Now.Minute * 60 + Context.Scheduler.Now.Second) % (15 * 60) / 60d;

        if (remainingMinutesThisQuarter < 0.5)
            remainingMinutesThisQuarter = 0.5;

        var adjustedLoadForRemainingTime = 15 / remainingMinutesThisQuarter * availableLoadToReachPeak;

        Context.Logger.LogDebug($"Peak this month: {peakUsageThisMonth}W. Average load this quarter: {currentAverageDemand}W. Available load to reach peak: {availableLoadToReachPeak}W. Remaining minutes this quarter: {remainingMinutesThisQuarter:N1}. Adjusted load for remaining time: {adjustedLoadForRemainingTime:#####}W. ");
        var adjustedCurrent = Math.Round((netGridUsage - adjustedLoadForRemainingTime) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity);
        Context.Logger.LogDebug($"Adjusted current: {adjustedCurrent}A");

        return adjustedCurrent;
    }

    public double GetNearPeakAdjustedGridCurrent(double netGridUsage, double peakUsageThisMonth)
    {
        var currentDifference = (netGridUsage - peakUsageThisMonth) / TotalVoltage;

        return currentDifference is < 0 and > -1.20d
            ? 0
            : Math.Round(currentDifference, 0, MidpointRounding.ToPositiveInfinity);
    }

    public double GetMidPeakAdjustedGridCurrent(double trailingNetGridUsage, double peakUsageThisMonth)
    {
        var currentDifference = (trailingNetGridUsage - peakUsageThisMonth / 2) / TotalVoltage;

        return currentDifference is < 0.70 and > -0.70d
            ? 0
            : Math.Round(currentDifference, 0);
    }

    private double GetSolarPreferredAdjustedGridCurrent(double trailingNetGridUsage)
    {
        var currentDifference = trailingNetGridUsage / TotalVoltage;

        return currentDifference is < 1.15d and > -0.20d
            ? 0
            : Math.Round(currentDifference, 0, MidpointRounding.ToNegativeInfinity);
    }

    private double GetMidpointAdjustedGridCurrent(double trailingNetGridUsage)
    {
        var currentDifference = trailingNetGridUsage / TotalVoltage;

        return currentDifference is < 0.70d and > -0.70d
            ? 0
            : Math.Round(currentDifference, 0);
    }

    private double GetSolarOnlyAdjustedGridCurrent(double trailingNetGridUsage)
    {
        var currentDifference = trailingNetGridUsage / TotalVoltage;

        return currentDifference is < 0.20 and > -1.15d
            ? 0
            : Math.Round(currentDifference, 0, MidpointRounding.ToPositiveInfinity);
    }

    private double GetSolarSurplusAdjustedGridCurrent(double trailingNetGridUsage)
    {
        var currentDifference = trailingNetGridUsage / TotalVoltage + 1;

        return currentDifference is < 0.20 and > -1.15d
            ? 0
            : Math.Round(currentDifference, 0, MidpointRounding.ToPositiveInfinity);
    }
    private (double chargercurrent, bool changed) SetChargerCurrent(Car? connectedCar, double chargerCurrent, double currentCurrent)
    {
        if (chargerCurrent < MinimumCurrent)
            chargerCurrent = MinimumCurrent;

        if (chargerCurrent > MaximumCurrent)
            chargerCurrent = MaximumCurrent;

        if (connectedCar?.CanSetCurrent ?? false)
            chargerCurrent = MaximumCurrent;

        var netCurrentChange = chargerCurrent - currentCurrent;

        if (netCurrentChange == 0)
            return (chargerCurrent, false);

        ChangeCurrent(chargerCurrent);

        return (chargerCurrent, true);
    }

    private (double carCurrent, bool changed) SetCarCurrentIfSupported(double carCurrent, double currentCarCurrent)
    {
        if (!(ConnectedCar?.CanSetCurrent ?? false))
            return (carCurrent, false);

        if (carCurrent < ConnectedCar.MinimumCurrent)
            carCurrent = ConnectedCar.MinimumCurrent ?? 0;

        if (carCurrent > ConnectedCar.MaximumCurrent)
            carCurrent = ConnectedCar.MaximumCurrent ?? 0;

        var netCurrentChange = carCurrent - currentCarCurrent;

        if (netCurrentChange == 0)
            return (carCurrent, false);

        ConnectedCar.ChangeCurrent(carCurrent);

        return (carCurrent, true);
    }

    private void ChangeCurrent(double toBeCurrent)
    {
        if (_lastCurrentChange?.Add(TimeSpan.FromSeconds(5)) > Context.Scheduler.Now)
            return;

        _lastCurrentChange = Context.Scheduler.Now;
        HomeAssistant.CurrentNumber.Change(toBeCurrent);
    }

    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {
        var connectedCarNeedsEnergy = ConnectedCar?.NeedsEnergy ?? false;
        var needsEnergy = (HomeAssistant.StateSensor.State == CarChargerStates.Occupied.ToString() || HomeAssistant.StateSensor.State == CarChargerStates.Charging.ToString()) && connectedCarNeedsEnergy;

        if (IsRunning && !needsEnergy)
            TurnOff();

        //Turns off entire charging station if no known car is connected that needs energy.
        if (!IsRunning && !needsEnergy && (HomeAssistant.CurrentNumber.State ?? 0) > OffCurrent)
            TurnOff();
    }


    protected override EnergyConsumerState GetState()
    {
        var connectedCarNeedsEnergy = ConnectedCar?.NeedsEnergy ?? false;
        var needsEnergy = (HomeAssistant.StateSensor.State == CarChargerStates.Occupied.ToString() || HomeAssistant.StateSensor.State == CarChargerStates.Charging.ToString()) && connectedCarNeedsEnergy;

        return IsRunning switch
        {
            true when !needsEnergy => EnergyConsumerState.Off,
            true => EnergyConsumerState.Running,
            false when needsEnergy && HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when needsEnergy => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };
    }

    protected override bool CanStart()
    {
        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (HasTimeWindow() && !IsWithinTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(State.LastRun?.Add(MinimumTimeout.Value) > Context.Scheduler.Now);
    }

    public override bool CanForceStop()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn())
            return false;

        if (HomeAssistant.CriticallyNeededSensor?.EntityState?.LastUpdated?.AddMinutes(3) > Context.Scheduler.Now)
            return false;

        if (BalancingMethod is BalancingMethod.MaximizeQuarterPeak or BalancingMethod.NearPeak or BalancingMethod.MidPeak)
            return false;

        if (_balancingMethodLastChangedAt?.AddMinutes(3) > Context.Scheduler.Now)
            return false;

        if (_lastCurrentChange?.AddMinutes(3) > Context.Scheduler.Now || ConnectedCar?.LastCurrentChange?.AddMinutes(3) > Context.Scheduler.Now)
            return false;

        if (CurrentCurrentForConnectedCar > MinimumCurrentForConnectedCar)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        //Can re-balance
        if (CurrentCurrentForConnectedCar > MinimumCurrentForConnectedCar)
            return false;

        //Just rebalanced, give it one cycle (might need more time as peak load is validated over past 3 minutes, hence * 2 for the moment
        if (_lastCurrentChange?.Add(MinimumRebalancingInterval * 2) > Context.Scheduler.Now || ConnectedCar?.LastCurrentChange?.Add(MinimumRebalancingInterval * 2) > Context.Scheduler.Now)
            return false;

        return true;
    }


    public override async void TurnOn()
    {
        try
        {
            if (ConnectedCar?.HomeAssistant.ChargerSwitch != null)
                ConnectedCar.TurnOnCharger();

            if (ConnectedCar?.CanSetCurrent ?? false)
            {
                ChangeCurrent(MaximumCurrent);
                ConnectedCar?.ChangeCurrent(ConnectedCar?.MinimumCurrent ?? 1);
            }
            else
            {
                ChangeCurrent(MinimumCurrent);
            }

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error while turning off car charger.");
        }
    }

    public override async void TurnOff()
    {
        try
        {
            if (ConnectedCar?.HomeAssistant.ChargerSwitch != null)
            {
                ConnectedCar.TurnOffCharger();
            }
            else
            {
                ChangeCurrent(OffCurrent);
            }

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error while turning off car charger.");
        }
    }

    private void CurrentNumber_Changed(object? sender, Entities.Input.InputNumberSensorEventArgs e)
    {
        if (e.New.State < MinimumCurrent)
        {
            if (State.State == EnergyConsumerState.Running)
                Stopped();

            return;
        }

        if (ConnectedCar?.HomeAssistant.ChargerSwitch == null)
        {
            if (State.State != EnergyConsumerState.Running)
                TurnOn(); //This does not seem to make sense, should call Started() instead?

            return;
        }

        if (State.State != EnergyConsumerState.Running && ConnectedCar.HomeAssistant.ChargerSwitch.IsOn())
            TurnOn();//This does not seem to make sense, should call Started() instead?
    }

    private void Car_ChargerTurnedOff(object? sender, BinarySensorEventArgs e)
    {
        Context.Logger.LogInformation($"Car '{ConnectedCar?.Name}' charger switch turned off.");

        if (State.State == EnergyConsumerState.Running)
            Stopped();
    }

    private void Car_ChargerTurnedOn(object? sender, BinarySensorEventArgs e)
    {
        Context.Logger.LogInformation($"Car '{ConnectedCar?.Name}' charger switch turned on.");

        //Debounce with Car_Connected
        if (ConnectedCar?.AutoPowerOnWhenConnecting ?? false)
            TurnOn();
    }
    private void Car_Connected(object? sender, BinarySensorEventArgs e)
    {
        Context.Logger.LogInformation($"Car '{ConnectedCar?.Name}' connected.");

        //Debounce with Car_ChargerTurnedOn
        if (ConnectedCar?.AutoPowerOnWhenConnecting ?? false)
            TurnOn();
    }

    public override void Dispose()
    {
        HomeAssistant.CurrentNumber.Changed -= CurrentNumber_Changed;
        HomeAssistant.Dispose();

        MqttSensors.BalancingMethodChanged -= BalancingMethodChanged;
        MqttSensors.BalanceOnBehalfOfChanged -= BalanceOnBehalfOfChanged;
        MqttSensors.AllowBatteryPowerChanged -= AllowBatteryPowerChanged;
        MqttSensors.Dispose();

        foreach (var car in Cars)
        {
            car.ChargerTurnedOn -= Car_ChargerTurnedOn;
            car.ChargerTurnedOff -= Car_ChargerTurnedOff;
            car.Connected -= Car_Connected;
            car.Dispose();
        }

        ConsumptionMonitorTask?.Dispose();
    }
}