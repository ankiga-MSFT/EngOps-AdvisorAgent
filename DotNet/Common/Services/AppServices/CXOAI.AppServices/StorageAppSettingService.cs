using Newtonsoft.Json;
using Provider.Interfaces;

namespace CXOAI.AppServices;

public class StorageAppSettingService : IStorageAppSettingService
{
    private readonly IAzureStorageProvider _storageProvider;
    private readonly string _containerName;

    public StorageAppSettingService(
        IAzureStorageProvider storageProvider,
        string containerName = AppSettingConstants.DefaultContainerName)
    {
        _storageProvider = storageProvider;
        _containerName = containerName;
    }

    public async Task<Dictionary<string, string>> ReadConfigAsync(string blobFileName)
    {
        var configDictionary = new Dictionary<string, string>();
        try
        {
#if DEBUG
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), AppSettingConstants.EnvironmentSettingsFolderName, blobFileName);
            var jsonContent = await File.ReadAllTextAsync(configPath);
            configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent, new StringDictionaryConverter());
#else
            var jsonContent = await _storageProvider.DownloadBlobAsync(_containerName, blobFileName);
            configDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent, new StringDictionaryConverter());
#endif
            return configDictionary!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read configuration.", ex);
        }
    }
}
