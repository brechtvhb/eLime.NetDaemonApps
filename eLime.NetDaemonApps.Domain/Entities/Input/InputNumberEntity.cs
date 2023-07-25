using NetDaemon.HassModel.Entities;
using NetDaemon.HassModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eLime.NetDaemonApps.Domain.Entities.Lights;

namespace eLime.NetDaemonApps.Domain.Entities.Input;

public record InputNumberEntity : NumericEntity<InputNumberEntity, NumericEntityState<InputNumberAttributes>, InputNumberAttributes>
{
    public InputNumberEntity(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public InputNumberEntity(Entity entity) : base(entity)
    {
    }

    public void Change(Double value)
    {
        CallService("set_value", new InputNumberSetValueParameters { Value = value });
    }

}