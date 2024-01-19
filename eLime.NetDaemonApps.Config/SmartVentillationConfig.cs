namespace eLime.NetDaemonApps.Config;

public class SmartVentilationConfig
{
    public string Name { get; set; }
    public bool? Enabled { get; set; }

    public IndoorAirQualityGuardConfig? Indoor { get; set; }
}


public class StatePingPongGuardConfig
{
    public TimeSpan TimeoutSpan { get; set; }
}

public class OutdoorVentilationGuardConfig
{
    //TODO: PM2.5 & NOX
}


public class IndoorAirQualityGuardConfig
{
    public IList<string>? Co2Sensors { get; set; }
    public Int32 Co2MediumThreshold { get; set; }
    public Int32 Co2HighThreshold { get; set; }

    //TODO: PM2.5
}

public class BathroomAirQualityGuardConfig
{
    public IList<string>? HumiditySensors { get; set; }
    public Int32 HumidityMediumThreshold { get; set; }
    public Int32 HumidityHighThreshold { get; set; }
}

public class MoldGuardConfig
{
    public TimeSpan MaxAwayTimeSpan { get; set; }
    public TimeSpan RechargeTimeSpan { get; set; }

}

public class DryAirGuardConfig
{
    public IList<string>? HumiditySensors { get; set; }
    public Int32 HumidityLowThreshold { get; set; }

    public string? OutdoorTemperatureSensor { get; set; }
    public Int32 MaxOutdoorTemperature { get; set; }
}

public class IndoorTemperatureGuardConfig
{
    public string? SummerModeSensor { get; set; }
    public string? OutdoorTemperatureSensor { get; set; }
    public string? HeatExchangerTemperatureSensor { get; set; }

    //VentilationEntity
}
public class ElectricityBillGuardConfig
{
    public string? AwaySensor { get; set; }
    public string? SleepingSensor { get; set; }
}