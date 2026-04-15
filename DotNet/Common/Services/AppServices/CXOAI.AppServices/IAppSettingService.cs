namespace CXOAI.AppServices;

public interface IAppSettingService
{
    Dictionary<string, string> Configuration { get; }
}
