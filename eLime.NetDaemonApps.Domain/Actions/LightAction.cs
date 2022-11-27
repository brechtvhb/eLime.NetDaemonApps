﻿using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Lights;
using Action = eLime.NetDaemonApps.Domain.Rooms.Actions.Action;

namespace eLime.NetDaemonApps.Domain.Actions;

public abstract class LightAction : Action
{

    public List<Light> Lights { get; init; }
    public TimeSpan? TransitionDuration { get; init; }
    public TimeSpan? AutoTransitionDuration { get; init; }

    protected LightAction(List<Light> lights, TimeSpan? transitionDuration, TimeSpan? autoTransitionDuration)
    {
        Lights = lights;
        TransitionDuration = transitionDuration;
        AutoTransitionDuration = autoTransitionDuration;
    }

}

public class LightTurnOnAction : LightAction
{
    public string? Profile { get; init; }
    public (int hue, int saturation)? HsColor { get; init; }
    public (int x, int y)? XyColor { get; init; }
    public (int red, int green, int blue)? RgbColor { get; init; }
    public (int red, int green, int blue, int white)? RgbwColor { get; init; }
    public (int red, int green, int blue, int coldWhite, int warmWhite)? RgbwwColor { get; init; }
    public int? ColorTempKelvin { get; init; }
    public int? ColorTempMireds { get; init; }
    public string? ColorName { get; init; }
    public int? Brightness { get; init; }
    public int? BrightnessPct { get; init; }
    public int? BrightnessStep { get; init; }
    public int? BrightnessStepPct { get; init; }
    public int? White { get; init; }
    public string? Flash { get; init; }
    public string? Effect { get; init; }

    public LightTurnOnAction(List<Light> lights, TimeSpan? transitionDuration, TimeSpan? autoTransitionDuration, String? profile,
        Color? color, String? brightness, string? flash, string? effect)
        : base(lights, transitionDuration, autoTransitionDuration)
    {
        Profile = profile;

        switch (color)
        {
            case null:
                break;
            case var _ when !String.IsNullOrWhiteSpace(color.Name):
                ColorName = color.Name;
                break;
            case var _ when color.Hue != null && color.Saturation != null:
                HsColor = (color.Hue.Value, color.Saturation.Value);
                break;
            case var _ when color.X != null && color.Y != null:
                XyColor = (color.X.Value, color.Y.Value);
                break;
            case var _ when color.Red != null && color.Green != null && color.Blue != null && color.ColdWhite != null && color.WarmWhite != null:
                RgbwwColor = (color.Red.Value, color.Green.Value, color.Blue.Value, color.ColdWhite.Value, color.WarmWhite.Value);
                break;
            case var _ when color.Red != null && color.Green != null && color.Blue != null && color.White != null:
                RgbwColor = (color.Red.Value, color.Green.Value, color.Blue.Value, color.White.Value);
                break;
            case var _ when color.Red != null && color.Green != null && color.Blue != null:
                RgbColor = (color.Red.Value, color.Green.Value, color.Blue.Value);
                break;
            case var _ when color.Kelvin != null:
                ColorTempKelvin = color.Kelvin;
                break;
            case var _ when color.Mireds != null:
                ColorTempMireds = color.Mireds;
                break;
        }

        switch (brightness)
        {
            case null:
            case var _ when String.IsNullOrWhiteSpace(brightness):
                break;
            case var _ when brightness.StartsWith("+") && brightness.EndsWith("%"):
                BrightnessStepPct = Convert.ToInt32(brightness[1..^1]);
                break;
            case var _ when brightness.StartsWith("-") && brightness.EndsWith("%"):
                BrightnessStepPct = Convert.ToInt32(brightness[..^1]);
                break;
            case var _ when brightness.EndsWith("%"):
                BrightnessPct = Convert.ToInt32(brightness[..^1]);
                break;
            case var _ when brightness.StartsWith("+"):
                BrightnessStep = Convert.ToInt32(brightness[1..]);
                break;
            case var _ when brightness.StartsWith("-"):
                BrightnessStep = Convert.ToInt32(brightness);
                break;
            case var _:
                Brightness = Convert.ToInt32(brightness);
                break;
        }

        Flash = flash;
        Effect = effect;
    }
    public override Task Execute(Boolean isAutoTransition = false)
    {
        var transitionDuration = (long?)(isAutoTransition ? AutoTransitionDuration ?? TransitionDuration : TransitionDuration)?.TotalSeconds;
        Lights.TurnOn(new LightTurnOnParameters
        {
            Transition = transitionDuration,
            Profile = Profile,
            HsColor = HsColor != null ? new List<int> { HsColor.Value.hue, HsColor.Value.saturation } : null,
            XyColor = XyColor != null ? new List<int> { XyColor.Value.x, XyColor.Value.y } : null,
            RgbColor = RgbColor != null ? new List<int> { RgbColor.Value.red, RgbColor.Value.green, RgbColor.Value.blue } : null,
            RgbwColor = RgbwColor != null ? new List<int> { RgbwColor.Value.red, RgbwColor.Value.green, RgbwColor.Value.blue, RgbwColor.Value.white } : null,
            RgbwwColor = RgbwwColor != null ? new List<int> { RgbwwColor.Value.red, RgbwwColor.Value.green, RgbwwColor.Value.blue, RgbwwColor.Value.coldWhite, RgbwwColor.Value.warmWhite } : null,
            Kelvin = ColorTempKelvin,
            ColorTemp = ColorTempMireds,
            ColorName = ColorName,
            Brightness = Brightness,
            BrightnessPct = BrightnessPct,
            BrightnessStep = BrightnessStep,
            BrightnessStepPct = BrightnessStepPct,
            White = White,
            Flash = Flash,
            Effect = Effect
        });
        return Task.CompletedTask;
    }
}

public class LightTurnOffAction : LightAction
{

    public LightTurnOffAction(List<Light> lights, TimeSpan? transitionDuration, TimeSpan? autoTransitionDuration)
        : base(lights, transitionDuration, autoTransitionDuration)
    {

    }
    public override Task Execute(Boolean isAutoTransition = false)
    {
        var transitionDuration = (isAutoTransition ? AutoTransitionDuration ?? TransitionDuration : TransitionDuration)?.TotalSeconds;
        Lights.TurnOff(new LightTurnOffParameters
        {
            Transition = transitionDuration
        });
        return Task.CompletedTask;
    }
}