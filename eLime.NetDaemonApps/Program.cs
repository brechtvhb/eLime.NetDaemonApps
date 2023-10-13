using eLime.NetDaemonApps.apps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;
using System.Reflection;

#pragma warning disable CA1812

try
{
    await Host.CreateDefaultBuilder(args)
            .RegisterAppSettingsJsonToHost()
            //Silly NetDaemon
            .ConfigureHostConfiguration(config =>
            {
                config.AddJsonFile($"appsettings.development.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables();
            })
            .RegisterYamlSettings()
            .ConfigureServices((context, services)
                => services.ConfigureNetDaemonServices(context.Configuration)
            )
        .AddFileStorage()
        .UseNetDaemonDefaultLogging()
        .UseNetDaemonRuntime()
        .UseNetDaemonTextToSpeech()
        .UseNetDaemonMqttEntityManagement()
        .ConfigureServices((_, services) =>
            services
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
        )
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to start host... {e}");
    throw;
}