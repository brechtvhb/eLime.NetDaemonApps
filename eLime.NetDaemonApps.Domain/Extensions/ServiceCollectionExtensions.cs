using Microsoft.Extensions.DependencyInjection;

namespace eLime.NetDaemonApps.Domain.Extensions
{
    public static class MediatrDependencyHandler
    {
        public static IServiceCollection RegisterRequestHandlers(this IServiceCollection services)
        {
            return services.AddMediatR(cf => cf.RegisterServicesFromAssembly(typeof(MediatrDependencyHandler).Assembly));
        }
    }
}
