using Conesoft.Blazor.NetatmoAuth.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Conesoft.Blazor.NetatmoAuth;

public static class ServiceExtension
{
    public static IServiceCollection AddNetatmoTokenStorageOnDisk<Storage>(this IServiceCollection services) where Storage : class, IStorage
    {
        return services.AddKeyedSingleton<IStorage, Storage>("netatmo");
    }
}