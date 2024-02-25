using Microsoft.Extensions.DependencyInjection;

namespace Conesoft.Blazor.NetatmoAuth;

public static class ServiceExtension
{
    public static IServiceCollection AddNetatmoTokenStorageOnDisk(this IServiceCollection services, Func<string, string> pathGenerator)
    {
        return services.AddKeyedSingleton<IStorage>("netatmo", new DiskStorage(pathGenerator));
    }
}