namespace eLime.NetDaemonApps.Config.FlexiLights;

public class ActionConfig
{
    public bool ExecuteOffActions { get; set; }
    public string? Light { get; set; }
    public LightAction LightAction { get; set; }
    public string? Scene { get; set; }
    public string? Script { get; set; }
    public Dictionary<String, String>? ScriptData { get; set; }
    public string? Profile { get; set; }
    public Color? Color { get; set; }
    public string? Brightness { get; set; }
    public string? Flash { get; set; }
    public string? Effect { get; set; }

    public string? Switch { get; set; }
    public SwitchAction SwitchAction { get; set; }
    public TimeSpan? PulseDuration { get; set; }
}

public class Color
{
    public string? Name { get; set; }
    public int? Hue { get; set; }
    public int? Saturation { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Red { get; set; }
    public int? Green { get; set; }
    public int? Blue { get; set; }
    public int? White { get; set; }
    public int? ColdWhite { get; set; }
    public int? WarmWhite { get; set; }
    public int? Kelvin { get; set; }
    public int? Mireds { get; set; }
}

public enum LightAction
{
    Unknown,
    TurnOn,
    TurnOff
}
public enum SwitchAction
{
    Unknown,
    TurnOn,
    TurnOff,
    Toggle,
    Pulse
}