namespace CXOAI.AppServices;

public interface IStorageAppSettingService
{
    Task<Dictionary<string, string>> ReadConfigAsync(string blobFileName);
}
