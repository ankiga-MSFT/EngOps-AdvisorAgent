namespace CXOAI.AppServices;

public class AppSettingService : IAppSettingService
{
    private readonly Dictionary<string, string> _configuration;

    public AppSettingService(Dictionary<string, string> configuration)
    {
        _configuration = configuration;
    }

    public Dictionary<string, string> Configuration => _configuration;
}
