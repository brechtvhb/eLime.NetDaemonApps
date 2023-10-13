using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace eLime.NetDaemonApps.apps;

static class StorageExtensions
{
    public static IHostBuilder AddFileStorage(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IFileStorage, FileStorage>();
        });
    }
}