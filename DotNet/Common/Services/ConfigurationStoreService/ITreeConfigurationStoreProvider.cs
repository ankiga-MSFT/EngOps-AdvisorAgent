using Azure;
using Azure.Search.Documents.Models;

namespace CXOAI.ConfigurationStore
{
    public interface ITreeConfigurationStoreProvider
    {
        Task<List<TreeConfiguration>> GetConfigurations(string componentName, bool needNestedConfigs);
        Task<List<TreeConfiguration>> GetConfigurationsWithDescription(string componentName, string searchText, bool needNestedConfigs);
        Task<List<TreeConfiguration>> GetConfigurationsWithNames(string componentName, List<string> configurationNames, bool needNestedConfigs);
        Task<Response<IndexDocumentsResult>> UploadDocumentAsync(TreeConfiguration configStore);
    }
}