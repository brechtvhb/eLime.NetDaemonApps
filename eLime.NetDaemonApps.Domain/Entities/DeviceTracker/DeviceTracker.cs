using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.DeviceTracker;


public record DeviceTracker : Entity<DeviceTracker, EntityState<DeviceTrackerAttributes>, DeviceTrackerAttributes>, IDeviceTrackerEntityCore
{
    public DeviceTracker(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public DeviceTracker(IEntityCore entity) : base(entity)
    {
    }
}