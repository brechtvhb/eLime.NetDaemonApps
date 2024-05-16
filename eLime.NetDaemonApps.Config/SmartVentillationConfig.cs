using eLime.NetDaemonApps.Config.SmartVentilation;

namespace eLime.NetDaemonApps.Config;

public class SmartVentilationConfig
{
    public string Name { get; set; }
    public bool? Enabled { get; set; }
    public String NetDaemonUserId { get; set; }

    public string ClimateEntity { get; set; }

    public StatePingPongGuardConfig StatePingPong { get; set; }
    public IndoorAirQualityGuardConfig Indoor { get; set; }
    public BathroomAirQualityGuardConfig Bathroom { get; set; }
    public IndoorTemperatureGuardConfig IndoorTemperature { get; set; }
    public MoldGuardConfig Mold { get; set; }
    public DryAirGuardConfig DryAir { get; set; }
    public ElectricityBillGuardConfig ElectricityBill { get; set; }

}