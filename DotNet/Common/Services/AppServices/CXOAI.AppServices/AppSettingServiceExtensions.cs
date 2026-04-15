using Microsoft.Extensions.DependencyInjection;
using Provider.Interfaces;

namespace CXOAI.AppServices;

public static class AppSettingServiceExtensions
{
    /// <summary>
    /// Registers IStorageAppSettingService and IAppSettingService in DI.
    /// Reads the environment-specific config file at startup.
    /// 
    /// In DEBUG: reads from local EnvironmentSettings/{env}.environment.settings.json
    /// In RELEASE: reads from Azure Blob Storage via IAzureStorageProvider.
    /// 
    /// Requires IAzureStorageProvider to be registered in DI before calling this.
    /// </summary>
    public static IServiceCollection AddCxoaiAppSettings(
        this IServiceCollection services,
        string containerName = AppSettingConstants.DefaultContainerName)
    {
        services.AddSingleton<IStorageAppSettingService>(sp =>
        {
            var storageProvider = sp.GetRequiredService<IAzureStorageProvider>();
            return new StorageAppSettingService(storageProvider, containerName);
        });

        services.AddSingleton<IAppSettingService>(sp =>
        {
            var storageService = sp.GetRequiredService<IStorageAppSettingService>();

            var env = Environment.GetEnvironmentVariable(AppSettingConstants.EnvironmentVariableName)
                      ?? "test";
            var blobFileName = $"{env.ToLower()}.environment.settings.json";

            var configDictionary = storageService.ReadConfigAsync(blobFileName).GetAwaiter().GetResult();

            return new AppSettingService(configDictionary);
        });

        return services;
    }
}
