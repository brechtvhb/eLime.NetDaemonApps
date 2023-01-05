using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.BinarySensors;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FlexiScreen
{
    public string? Name { get; }
    private EnabledSwitch EnabledSwitch { get; set; }

    public FlexiScreen(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, FlexiScreenConfig config)
    {
    }
}