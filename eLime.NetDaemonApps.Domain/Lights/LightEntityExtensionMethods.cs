using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Lights;

public static class LightEntityExtensionMethods
{
    ///<summary>Toggles one or more lights, from on to off, or, off to on, based on their current state. </summary>
    public static void Toggle(this IEnumerable<Light> target, LightToggleParameters data)
    {
        target.CallService("toggle", data);
    }


    ///<summary>Toggles one or more lights, from on to off, or, off to on, based on their current state. </summary>
    ///<param name="target">The IEnumerable<LightEntity> to call this service for</param>
    ///<param name="transition">Duration it takes to get to next state.</param>
    ///<param name="rgbColor">Color for the light in RGB-format. eg: [255, 100, 100]</param>
    ///<param name="colorName">A human readable color name.</param>
    ///<param name="hsColor">Color for the light in hue/sat format. Hue is 0-360 and Sat is 0-100. eg: [300, 70]</param>
    ///<param name="xyColor">Color for the light in XY-format. eg: [0.52, 0.43]</param>
    ///<param name="colorTemp">Color temperature for the light in mireds.</param>
    ///<param name="kelvin">Color temperature for the light in Kelvin.</param>
    ///<param name="whiteValue">Number indicating level of white.</param>
    ///<param name="brightness">Number indicating brightness, where 0 turns the light off, 1 is the minimum brightness and 255 is the maximum brightness supported by the light.</param>
    ///<param name="brightnessPct">Number indicating percentage of full brightness, where 0 turns the light off, 1 is the minimum brightness and 100 is the maximum brightness supported by the light.</param>
    ///<param name="profile">Name of a light profile to use. eg: relax</param>
    ///<param name="flash">If the light should flash.</param>
    ///<param name="effect">Light effect.</param>
    public static void Toggle(this IEnumerable<Light> target, long? @transition = null, object? @rgbColor = null, string? @colorName = null, object? @hsColor = null, object? @xyColor = null, long? @colorTemp = null, long? @kelvin = null, long? @whiteValue = null, long? @brightness = null, long? @brightnessPct = null, string? @profile = null, string? @flash = null, string? @effect = null)
    {
        target.CallService("toggle", new LightToggleParameters { Transition = @transition, RgbColor = @rgbColor, ColorName = @colorName, HsColor = @hsColor, XyColor = @xyColor, ColorTemp = @colorTemp, Kelvin = @kelvin, WhiteValue = @whiteValue, Brightness = @brightness, BrightnessPct = @brightnessPct, Profile = @profile, Flash = @flash, Effect = @effect });
    }

    ///<summary>Turns off one or more lights.</summary>
    public static void TurnOff(this IEnumerable<Light> target, LightTurnOffParameters data)
    {
        target.CallService("turn_off", data);
    }

    ///<summary>Turns off one or more lights.</summary>
    ///<param name="target">The IEnumerable<LightEntity> to call this service for</param>
    ///<param name="transition">Duration it takes to get to next state.</param>
    ///<param name="flash">If the light should flash.</param>
    public static void TurnOff(this IEnumerable<Light> target, long? @transition = null, string? @flash = null)
    {
        target.CallService("turn_off", new LightTurnOffParameters { Transition = @transition, Flash = @flash });
    }

    ///<summary>Turn on one or more lights and adjust properties of the light, even when they are turned on already. </summary>
    public static void TurnOn(this IEnumerable<Light> target, LightTurnOnParameters data)
    {
        target.CallService("turn_on", data);
    }

    ///<summary>Turn on one or more lights and adjust properties of the light, even when they are turned on already. </summary>
    ///<param name="target">The IEnumerable<LightEntity> to call this service for</param>
    ///<param name="transition">Duration it takes to get to next state.</param>
    ///<param name="rgbColor">A list containing three integers between 0 and 255 representing the RGB (red, green, blue) color for the light. eg: [255, 100, 100]</param>
    ///<param name="rgbwColor">A list containing four integers between 0 and 255 representing the RGBW (red, green, blue, white) color for the light. eg: [255, 100, 100, 50]</param>
    ///<param name="rgbwwColor">A list containing five integers between 0 and 255 representing the RGBWW (red, green, blue, cold white, warm white) color for the light. eg: [255, 100, 100, 50, 70]</param>
    ///<param name="colorName">A human readable color name.</param>
    ///<param name="hsColor">Color for the light in hue/sat format. Hue is 0-360 and Sat is 0-100. eg: [300, 70]</param>
    ///<param name="xyColor">Color for the light in XY-format. eg: [0.52, 0.43]</param>
    ///<param name="colorTemp">Color temperature for the light in mireds.</param>
    ///<param name="kelvin">Color temperature for the light in Kelvin.</param>
    ///<param name="brightness">Number indicating brightness, where 0 turns the light off, 1 is the minimum brightness and 255 is the maximum brightness supported by the light.</param>
    ///<param name="brightnessPct">Number indicating percentage of full brightness, where 0 turns the light off, 1 is the minimum brightness and 100 is the maximum brightness supported by the light.</param>
    ///<param name="brightnessStep">Change brightness by an amount.</param>
    ///<param name="brightnessStepPct">Change brightness by a percentage.</param>
    ///<param name="white">Set the light to white mode and change its brightness, where 0 turns the light off, 1 is the minimum brightness and 255 is the maximum brightness supported by the light.</param>
    ///<param name="profile">Name of a light profile to use. eg: relax</param>
    ///<param name="flash">If the light should flash.</param>
    ///<param name="effect">Light effect.</param>
    public static void TurnOn(this IEnumerable<Light> target, long? @transition = null, object? @rgbColor = null, object? @rgbwColor = null, object? @rgbwwColor = null, string? @colorName = null, object? @hsColor = null, object? @xyColor = null, long? @colorTemp = null, long? @kelvin = null, long? @brightness = null, long? @brightnessPct = null, long? @brightnessStep = null, long? @brightnessStepPct = null, long? @white = null, string? @profile = null, string? @flash = null, string? @effect = null)
    {
        target.CallService("turn_on", new LightTurnOnParameters { Transition = @transition, RgbColor = @rgbColor, RgbwColor = @rgbwColor, RgbwwColor = @rgbwwColor, ColorName = @colorName, HsColor = @hsColor, XyColor = @xyColor, ColorTemp = @colorTemp, Kelvin = @kelvin, Brightness = @brightness, BrightnessPct = @brightnessPct, BrightnessStep = @brightnessStep, BrightnessStepPct = @brightnessStepPct, White = @white, Profile = @profile, Flash = @flash, Effect = @effect });
    }
}