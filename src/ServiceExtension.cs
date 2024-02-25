using Microsoft.Extensions.DependencyInjection;

namespace Conesoft.Blazor.NetdiscoAuth;

public static class ServiceExtension
{
    public static IServiceCollection AddNetatmoTokenStorageOnDisk(this IServiceCollection services, Func<string, string> pathGenerator)
    {
        return services.AddKeyedSingleton<IStorage>("netatmo", new DiskStorage(pathGenerator));
    }
}