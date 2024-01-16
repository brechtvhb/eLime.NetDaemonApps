namespace eLime.NetDaemonApps.Config;

public class SmartVentilationConfig
{
    public string Name { get; set; }
    public bool? Enabled { get; set; }

    public IndoorVentilationGuardConfig? Indoor { get; set; }
}


public class StatePingPongGuardConfig
{
    public TimeSpan TimeoutSpan { get; set; }
}

public class OutdoorVentilationGuardConfig
{
    //TODO: PM2.5 & NOX
}


public class IndoorVentilationGuardConfig
{
    public IList<string>? Co2Sensors { get; set; }
    public Int32 Co2MediumThreshold { get; set; }
    public Int32 Co2HighThreshold { get; set; }

    //TODO: PM2.5
}

public class BathroomVentilationGuardConfig
{
    public IList<string>? HumiditySensors { get; set; }
    public Int32 HumidityMediumThreshold { get; set; }
    public Int32 HumidityHighThreshold { get; set; }
}

public class MoldVentilationGuardConfig
{
    public TimeSpan MaxAwayTimeSpan { get; set; }
    public TimeSpan RechargeTimeSpan { get; set; }

}

public class DryAirVentilationGuardConfig
{
    public IList<string>? HumiditySensors { get; set; }
    public string? OutdoorTemperatureSensor { get; set; }
    public Int32 HumidityLowThreshold { get; set; }
    public Int32 MaxOutdoorTemperature { get; set; }
}

public class TemperatureVentilationGuardConfig
{
    public string? SummerModeSensor { get; set; }
    public string? OutdoorTemperatureSensor { get; set; }
    public string? HeatExchangerTemperatureSensor { get; set; }
}
public class ElectricityBillVentilationGuardConfig
{
    public string? AwaySensor { get; set; }
}