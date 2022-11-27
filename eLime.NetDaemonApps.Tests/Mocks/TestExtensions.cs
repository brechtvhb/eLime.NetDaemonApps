using NetDaemon.HassModel.Entities;
using System.Reflection;
using System.Text.Json;

namespace eLime.NetDaemonApps.Tests.Mocks;

public static class TestExtensions
{
    public static JsonElement AsJsonElement(this object value)
    {
        var jsonString = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(jsonString);
    }

    public static EntityState WithAttributes(this EntityState entityState, object attributes)
    {
        var copy = entityState with { };
        entityState.GetType().GetProperty("AttributesJson", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(copy, AsJsonElement(attributes));
        return copy;
    }
}