namespace eLime.NetDaemonApps.Domain.EnergyManager;

internal interface ICar
{
    string Name { get; }
    CarChargingMode ChargingMode { get; }
    (bool hasChargerSwitch, bool isCharging) IsCharging { get; }
    (bool canSetChargingCurrent, int chargingCurrent) ChargingCurrent { get; }
    int MinimumCurrent { get; }
    int MaximumCurrent { get; }
    DateTimeOffset? LastCurrentChange { get; }
    double BatteryCapacity { get; }
    bool RemainOnWhenMaxBatteryReached { get; }
    int BatteryPercentage { get; }
    (bool hasMaxBatteryPercentage, int maxBatteryPercentage) MaxBatteryPercentage { get; }
    bool IsCableConnected { get; }
    string Location { get; }
    bool AutoPowerOnWhenConnecting { get; }
    Boolean IsConnectedToHomeCharger { get; }
    Boolean NeedsEnergy { get; }
    Boolean IsRunning { get; }
    StartCarChargerCommand StartCharging();
    StartCarChargerCommand StopCharging();
    AdjustChargingCurrentCommand AdjustCurrent(int current, DateTime now);
    StartCarChargerCommand CableConnected();
    StartCarChargerCommand LocationChanged();
}

internal class Car2 : ICar
{
    public string Name { get; private init; }
    public CarChargingMode ChargingMode { get; private init; }

    private Func<bool?> IsChargingFunc { get; init; }

    public (bool hasChargerSwitch, bool isCharging) IsCharging
    {
        get
        {
            var result = IsChargingFunc();
            return (result == null, result ?? false);
        }
    }

    private Func<int?> ChargingCurrentFunc { get; init; }
    public (bool canSetChargingCurrent, int chargingCurrent) ChargingCurrent
    {
        get
        {
            var result = ChargingCurrentFunc();
            return (result == null, result ?? 0);
        }
    }

    public int MinimumCurrent { get; private init; }
    public int MaximumCurrent { get; private set; }
    public DateTimeOffset? LastCurrentChange { get; private set; }

    public double BatteryCapacity { get; private set; }
    public bool RemainOnWhenMaxBatteryReached { get; private init; }
    private Func<int> BatteryPercentageFunc { get; init; }
    public int BatteryPercentage => BatteryPercentageFunc();

    private Func<int?> MaxBatteryPercentageFunc { get; init; }
    public (bool hasMaxBatteryPercentage, int maxBatteryPercentage) MaxBatteryPercentage
    {
        get
        {
            var result = MaxBatteryPercentageFunc();
            return (result == null, result ?? 0);
        }
    }

    private Func<bool> IsCableConnectedFunc { get; init; }
    public bool IsCableConnected => IsCableConnectedFunc();

    private Func<string> LocationFunc { get; init; }
    public string Location => LocationFunc();
    public bool AutoPowerOnWhenConnecting { get; private set; }

    public Car2(string name, CarChargingMode chargingMode, Func<bool?> isChargingFunc, Func<int?> chargingCurrentFunc, int minimumCurrent, int maximumCurrent,
        double batteryCapacity, Func<int> batteryPercentageFunc, Func<int?> maxBatteryPercentageFunc, bool remainOnWhenMaxBatteryReached,
        Func<bool> isCableConnectedFunc, bool autoPowerOnWhenConnecting, Func<string> locationFunc)
    {
        Name = name;
        ChargingMode = chargingMode;
        IsChargingFunc = isChargingFunc;
        ChargingCurrentFunc = chargingCurrentFunc;
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;

        BatteryCapacity = batteryCapacity;
        RemainOnWhenMaxBatteryReached = remainOnWhenMaxBatteryReached;
        BatteryPercentageFunc = batteryPercentageFunc;
        MaxBatteryPercentageFunc = maxBatteryPercentageFunc;

        IsCableConnectedFunc = isCableConnectedFunc;
        LocationFunc = locationFunc;
        AutoPowerOnWhenConnecting = autoPowerOnWhenConnecting;
    }

    public Boolean IsConnectedToHomeCharger => IsCableConnected && Location == "home";
    public Boolean NeedsEnergy
    {
        get
        {
            if (!IsConnectedToHomeCharger)
                return false;

            if (RemainOnWhenMaxBatteryReached)
                return true;

            var (hasMaxBatteryPercentage, maxBatteryPercentage) = MaxBatteryPercentage;
            if (!hasMaxBatteryPercentage)
                return true;

            var currentBatteryPercentage = BatteryPercentage;
            return currentBatteryPercentage < maxBatteryPercentage;
        }
    }

    public Boolean IsRunning
    {
        get
        {
            var (hasChargerSwitch, isCharging) = IsCharging;
            var (canSetCurrent, chargingCurrent) = ChargingCurrent;

            return (hasChargerSwitch, canSetCurrent) switch
            {
                (false, true) => chargingCurrent >= MinimumCurrent,
                (false, false) => true,
                (true, true) => isCharging && chargingCurrent >= MinimumCurrent,
                (true, false) => isCharging,
            };
        }
    }

    public StartCarChargerCommand StartCharging()
    {
        var (hasChargerSwitch, isCharging) = IsCharging;

        if (hasChargerSwitch && !isCharging)
            return new StartCarChargerCommand(Name);

        return null;
    }
    public StartCarChargerCommand StopCharging()
    {
        var (hasChargerSwitch, isCharging) = IsCharging;

        if (hasChargerSwitch && isCharging)
            return new StartCarChargerCommand(Name);

        return null;
    }

    public AdjustChargingCurrentCommand AdjustCurrent(int current, DateTime now)
    {
        var (canSetCurrent, chargingCurrent) = ChargingCurrent;

        if (LastCurrentChange?.Add(TimeSpan.FromSeconds(5)) > now)
            return null;

        if (!canSetCurrent || chargingCurrent == current)
            return null;

        LastCurrentChange = now;
        return new AdjustChargingCurrentCommand(Name, current);
    }

    public StartCarChargerCommand CableConnected() => IsConnectedToHomeCharger ? StartCharging() : null;
    public StartCarChargerCommand LocationChanged() => IsConnectedToHomeCharger ? StartCharging() : null;
}


public class StartCarChargerCommand(String CarName)
{
}

public class StopCarChargerCommand(String CarName)
{
}
public class AdjustChargingCurrentCommand(String CarName, double Current)
{
}
