using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Weather;

public record Weather : Entity<Weather, EntityState<WeatherAttributes>, WeatherAttributes>
{
    public Weather(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public Weather(Entity entity) : base(entity)
    {
    }
}