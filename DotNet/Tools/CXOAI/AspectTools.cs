using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools.Models;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CXOAI.Tools;

public class AspectTools : ToolBase
{
    private readonly ILogger<AspectTools> _logger;
    private readonly IUserAuthContext _authContext;
    private readonly ITreeConfigurationStoreProvider _storeProvider;
    private AspectToolsConfig? _aspectToolsConfig;
    private JArray? _globalFilters;
    private readonly Dictionary<string, JObject> _metricConfigCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient _httpClient = new();

    public AspectTools(
        ILogger<AspectTools> logger,
        ITreeConfigurationStoreProvider storeProvider,
        IUserAuthContext authContext,
        IToolStatusNotifier notifier) : base(notifier)
    {
        _logger = logger;
        _authContext = authContext;
        this._storeProvider = storeProvider;

        
    }

    /// <summary>
    /// Loads AspectTool environment settings from the configuration store and binds to AspectToolsConfig.
    /// </summary>
    private async Task InitializeConfig()
    {
        try
        {
            _logger.LogInformation("Loading aspect skill environment settings.");

            var configurations = await _storeProvider.GetConfigurationsWithNames(
                CxOAIConstants.ConfigComponent_ToolConfiguration,
                new List<string> { CxOAIConstants.ConfigName_EnvironmentSettings },
                false);

            var configEntry = configurations?.FirstOrDefault();
            if (configEntry?.Configuration != null)
            {
                _aspectToolsConfig = JsonConvert.DeserializeObject<AspectToolsConfig>(configEntry.Configuration);
                await LoadCertificatesForAspectConfigs();
                _logger.LogInformation("AspectTool environment settings loaded successfully.");
            }
            else
            {
                _logger.LogWarning("AspectTool.EnvironmentSettings not found in configuration store.");
            }

            var globalFilterConfigs = await _storeProvider.GetConfigurationsWithNames(
                CxOAIConstants.ConfigComponent_ToolConfiguration,
                new List<string> { CxOAIConstants.ConfigName_GlobalFilters },
                false);

            var globalFilterEntry = globalFilterConfigs?.FirstOrDefault();
            if (globalFilterEntry?.Configuration != null)
            {
                var configJson = JObject.Parse(globalFilterEntry.Configuration);
                _globalFilters = configJson[CxOAIConstants.Field_Filters] as JArray;
                _logger.LogInformation("Global filters loaded: {Count} filters.", _globalFilters?.Count ?? 0);
            }
            else
            {
                _logger.LogWarning("AspectTool.GlobalFilters not found in configuration store.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AspectTool configuration from configuration store.");
        }
    }

    /// <summary>
    /// Downloads certificates from Azure Key Vault for each AspectApiConfig that has a ClientCertificateName.
    /// </summary>
    private async Task LoadCertificatesForAspectConfigs()
    {
        if (_aspectToolsConfig?.AspectsDetailsMap == null || string.IsNullOrWhiteSpace(_aspectToolsConfig.KeyVaultUrl))
            return;
        var credential =
#if DEBUG
            (Azure.Core.TokenCredential)new Azure.Identity.VisualStudioCredential();
#else
            (Azure.Core.TokenCredential)new Azure.Identity.ManagedIdentityCredential();
#endif
        var secretClient = new Azure.Security.KeyVault.Secrets.SecretClient(
            new Uri(_aspectToolsConfig.KeyVaultUrl), credential);

        foreach (var (domain, config) in _aspectToolsConfig.AspectsDetailsMap)
        {
            var certName = config.TokenAcquisitionConfig?.ClientCertificateName;
            if (!string.IsNullOrWhiteSpace(certName))
            {
                try
                {
                    var secret = await secretClient.GetSecretAsync(certName);
                    var certBytes = Convert.FromBase64String(secret.Value.Value);
                    config.TokenAcquisitionConfig!.Certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(certBytes, null);
                    _logger.LogInformation("Certificate '{CertName}' loaded for domain '{Domain}'.", certName, domain);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load certificate '{CertName}' for domain '{Domain}'.", certName, domain);
                }
            }
        }
    }

    #region Entity Recognition

    [Description("Retrieves the **Program Id** of the program from the provided **program name**. The function matches the input with the names defined in the ProgramTypeEnum. If a unique match is found, the **EXACT** Program Id of the matched program is returned in the format ch:group:{string?}:{string} within a CXOAgentResponse. Returns a CXOAgentResponse with 'NONE' if no match is found.")]
    public async Task<CXOAgentResponse> SearchProgramByProgramName(
        [Description("The program name.")] string programName)
    {
        _logger.LogInformation("Calling {MethodName} with parameter: {ProgramName}", nameof(SearchProgramByProgramName), programName);
        await NotifyAsync($"🔍 Resolving program '{programName}'...");

        try
        {
            // 1. Input Validation if workload type paramter itself is NONE
            if (string.IsNullOrWhiteSpace(programName))
            {
                _logger.LogWarning("No program name provided by the user.");
                return new CXOAgentResponse { IsSuccess = false, Response = "NONE" };
            }

            if(_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            // Normalize input.
            string normalizedInput = programName.Trim().ToLower();

            // Retrieve all enum members of ProgramTypeEnum with their EnumMappingAttribute.
            var enumType = typeof(ProgramTypeEnum);
            var enumMembers = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => new
                {
                    Name = f.Name,
                    Mapping = f.GetCustomAttributes(typeof(EnumMappingAttribute), false)
                               .Cast<EnumMappingAttribute>()
                               .FirstOrDefault()
                })
                .Where(x => x.Mapping != null)
                .ToList();

            // 2. Exact match: check if the input exactly matches the DisplayName or the enum member's name.
            var exactMatches = enumMembers.Where(x =>
                string.Equals(x.Mapping!.DisplayName, normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Name, normalizedInput, StringComparison.OrdinalIgnoreCase)).ToList();

            if (exactMatches.Count == 1)
            {
                await NotifyAsync("✅ Program resolved");
                return new CXOAgentResponse { IsSuccess = true, Response = exactMatches.First().Mapping!.GroupChUri };
            }
            else if (exactMatches.Count > 1)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = true,
                    Response = $"Multiple programs match your input. Please specify further: {string.Join(", ", exactMatches.Select(x => x.Mapping!.DisplayName))}."
                };
            }

            // Define custom mappings
            var customMappings = new Dictionary<string, ProgramTypeEnum>
            {
                { "pri0", ProgramTypeEnum.AzurePriority0 },
                { "prp", ProgramTypeEnum.ProactiveResilience },
                { "pri0 enhanced", ProgramTypeEnum.AzurePriority0Enhanced }
            };

            // 3. Custom Mapping Match
            if (customMappings.TryGetValue(normalizedInput, out var mappedEnum))
            {
                // Get the GroupChUri from the mapped enum's EnumMappingAttribute
                var enumField = typeof(ProgramTypeEnum).GetField(mappedEnum.ToString());
                var attribute = enumField?.GetCustomAttributes(typeof(EnumMappingAttribute), false)
                                         .Cast<EnumMappingAttribute>()
                                         .FirstOrDefault();
                var resolvedUri = attribute?.GroupChUri ?? mappedEnum.ToString();
                await NotifyAsync("✅ Program resolved");
                return new CXOAgentResponse { IsSuccess = true, Response = resolvedUri };
            }

            // 4. Substring matching: check if the DisplayName contains the input.
            var substringMatches = enumMembers.Where(x =>
                x.Mapping!.DisplayName.ToLower().Contains(normalizedInput)).ToList();

            if (substringMatches.Count == 1)
            {
                await NotifyAsync("✅ Program resolved");
                return new CXOAgentResponse { IsSuccess = true, Response = substringMatches.First().Mapping!.GroupChUri };
            }
            else if (substringMatches.Count > 1)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = true,
                    Response = $"Multiple programs match your input. Please specify further: {string.Join(", ", substringMatches.Select(x => x.Mapping!.DisplayName))}."
                };
            }

            // 5. Closest match using Levenshtein distance (threshold 3).
            var closestMatch = enumMembers
                .OrderBy(x => GetLevenshteinDistance(normalizedInput, x.Mapping!.DisplayName.ToLower()))
                .FirstOrDefault();

            if (closestMatch != null && GetLevenshteinDistance(normalizedInput, closestMatch.Mapping!.DisplayName.ToLower()) <= 3)
            {
                await NotifyAsync("✅ Program resolved");
                return new CXOAgentResponse { IsSuccess = true, Response = closestMatch.Mapping.GroupChUri };
            }
            else
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = true,
                    Response = "Unable to determine the program. Could you please clarify?"
                };
            }
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ unable to resolve program");
            _logger.LogError(ex, "Error in SearchProgramByProgramName for programName: {ProgramName}", programName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    [Description("""
        Azure customers can define a group of subscriptions which are very critical to them. They call this group a workload and it generally caters to a specific business scenario for the customer. These workloads can be of different types depending on the agreements the customer has with Azure around support offering, their relative importance to Azure business etc. A possible list of Azure workload types is defined in the WorkloadTypeEnum enum. This function tool maps a user's free-text input describing a workload type to the corresponding `WorkloadTypeEnum` member. This function tool returns the particular member of the WorkloadTypeEnum enum matching most closely to user request, or 'NONE' if no match is found. If the user has not specified any particular workload type description, then parameter should take the value 'NONE'. If there is any ambiguity default to 'NONE'
        **Examples:**
        1. **Input:** `"Priority Azure Services"`
           **Output:** `"AzurePriority0"`
        
        2. **Input:** `"Critical Infrastructure"`
           **Output:** `"Description may match multiple enum members. Please specify further: Mission Critical, Core Mission Critical."`
        
        3. **Input:** `"Unknown Type"`
           **Output:** `"NONE"`
        """)]
    public async Task<string> SearchCustomerWorkload(
        [Description("workloadType (string): Description of the workload type. If the user has not specified any particular workload type description, then parameter should take the value 'NONE'. If there is any ambiguity default to 'NONE'.")] string workloadType)
    {
        _logger.LogInformation("Calling {MethodName} with parameter: {WorkloadType}", nameof(SearchCustomerWorkload), workloadType);
        await NotifyAsync("🔍 Resolving workload type...");
        try
        {
            // 1. Input Validation if workload type paramter itself is NONE
            if (string.IsNullOrWhiteSpace(workloadType) || workloadType.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("No workload type provided by the user.");
                return "NONE"; // Default value indicating absence of workload type
            }

            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            // Define custom mappings
            var customMappings = new Dictionary<string, WorkloadTypeEnum>
            {
                { "pri0", WorkloadTypeEnum.AzurePriority0 },
                { "prp", WorkloadTypeEnum.ProactiveResilience },
                { "pri0 enhanced", WorkloadTypeEnum.AzurePriority0Enhanced }
            };

            // Retrieve all enum members with their descriptions
            var enumType = typeof(WorkloadTypeEnum);
            var enumMembers = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => new
                {
                    Name = f.Name,
                    Description = f.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                   .Cast<DescriptionAttribute>()
                                   .FirstOrDefault()?.Description ?? f.Name
                }).ToList();

            // Normalize user input
            string normalizedInput = workloadType.Trim().ToLower();

            // 2. Exact Match
            var exactMatch = enumMembers.FirstOrDefault(e =>
                e.Name.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                await NotifyAsync("✅ Workload type resolved");
                return exactMatch.Name;
            }

            // 3. Custom Mapping Match
            if (customMappings.TryGetValue(normalizedInput, out var mappedEnum))
            {
                await NotifyAsync("✅ Workload type resolved");
                return mappedEnum.ToString();
            }

            // 4. Substring Matching
            var substringMatches = enumMembers.Where(e =>
                e.Description.ToLower().Contains(normalizedInput)).ToList();

            if (substringMatches.Count == 1)
            {
                await NotifyAsync("✅ Workload type resolved");
                return substringMatches.First().Name;
            }
            else if (substringMatches.Count > 1)
            {
                return $"Multiple workload types match your input. Please specify further: {string.Join(", ", substringMatches.Select(m => m.Description))}.";
            }

            // 5. Levenshtein Distance with Threshold
            var closestMatch = enumMembers
                .OrderBy(e => GetLevenshteinDistance(normalizedInput, e.Description.ToLower()))
                .FirstOrDefault();

            if (closestMatch != null && GetLevenshteinDistance(normalizedInput, closestMatch.Description.ToLower()) <= 3)
            {
                await NotifyAsync("✅ Workload type resolved");
                return closestMatch.Name;
            }
            else
            {
                return "NONE"; // Return "NONE" if no match is found
            }
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ unable to resolve workload type");
            _logger.LogError(ex, "Error in SearchCustomerWorkload for workloadType: {WorkloadType}", workloadType);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    [Description("Retrieves the **Customer ID** and associated details based on the provided **Customer Name** and **Workload Type**. Use this function when the user is asking for information related to a **customer**, optionally with a specific **workload**. Only if the user mentions the word 'workload' in their query prompt with workloadType description, then invoke `AspectTools.SearchCustomerWorkload` function prior to calling this method, otherwise 'workloadType' should be \"NONE\". If multiple customer records are found, prompt the user to select the appropriate customer by displaying the detailed information. At the end, return the **EXACT** customer Id in the format ch:customer::tpid:{integer} when Workload Type is NONE or ch:customer:{guid}:id:{guid} when workload type is present.")]
    public async Task<CXOAgentResponse> SearchCustomerByNameAndWorkloadType(
        [Description("The customer name")] string customerName,
        [Description("The workload type associated with the customer. **Only if the user mentions the word 'workload' in their request, then this should be obtained by calling the `SearchCustomerWorkload` function beforehand and must be a valid member of the `WorkloadTypeEnum` enumeration. Otherwise 'workloadType' will be \"NONE\".**")] string workloadType)
    {
        try
        {
            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            await NotifyAsync($"🔍 Searching for customer '{customerName}'...");

            // TODO: Replace with ToolExecutionContext.Current.UserToken once ToolExecutionContext is implemented
            string userToken = _authContext.AccessToken!;

            var validWorkloadType = Enum.TryParse<WorkloadTypeEnum>(workloadType, ignoreCase: true, out var temp) ? temp : WorkloadTypeEnum.None;

            if (validWorkloadType == WorkloadTypeEnum.None)
            {
                _logger.LogInformation("Calling {MethodName} with parameters: {CustomerName}",
                    nameof(SearchCustomerByNameAndWorkloadType), customerName);

                // Split the customer name into words and process each word
                string customerNameSearchText = FormatCustomerSearchField(customerName);

                Stopwatch sw = Stopwatch.StartNew();

                var customerDomainAspectConfig = _aspectToolsConfig!.AspectsDetailsMap[CxOAIConstants.Domain_Customer];
                string customerSearchUrl = string.Format(CxOAIConstants.InsightsApiUrl, customerDomainAspectConfig.BaseUrl, "ch:special:all", "ch:aspect:search:azurecustomers");

                AspectInsightsPayload customerSearchPayload = new()
                {
                    SearchText = customerNameSearchText + "*",
                    SearchFields = new List<string>() { "EntityName" },
                    QueryType = "Full",
                    SearchMode = "All",
                    Top = 5,
                    OrderBy = new List<string>() { "Consumption desc" },
                    Select = new List<string>() { "EntityName", "TPID_SubscriptionsCount", "GroupNames", "EntityId" },
                    IncludeTotalResultCount = true
                };

                JObject searchResults = await CallInsightsServiceAsync(customerSearchUrl, customerDomainAspectConfig, userToken, customerSearchPayload, CxOAIConstants.Domain_Customer);

                sw.Stop();
                _logger.LogInformation("Time taken to search customers for input: '{CustomerName}' was Duration: {ElapsedMs}ms", customerName, sw.ElapsedMilliseconds);

                List<JObject> results = new();
                foreach (var searchResult in searchResults["Results"]!.ToObject<JArray>()!)
                {
                    JObject doc = searchResult["Document"]!.ToObject<JObject>()!;

                    results.Add(new JObject()
                    {
                        ["Customer Name"] = doc["EntityName"]!.ToString(),
                        ["Customer Id"] = $"EntityId: {doc["EntityId"]!.ToString()}",
                        ["Subscription Count"] = doc["TPID_SubscriptionsCount"]?.ToString(),
                        ["Programs"] = doc["GroupNames"]?.ToString(),
                        ["AgentPrompt"] = "Show Customer Name, Customer Id, Subscription Count and Programs."
                    });
                }

                return await BuildCustomerSearchResponseAsync(results, customerName);
            }
            else
            {
                _logger.LogInformation("Calling {MethodName} with parameters: {CustomerName} and {WorkloadType}",
                    nameof(SearchCustomerByNameAndWorkloadType), customerName, validWorkloadType);

                string customerNameSearchText = FormatCustomerSearchField(customerName);

                Stopwatch sw = Stopwatch.StartNew();

                var customerDomainAspectConfig = _aspectToolsConfig!.AspectsDetailsMap[CxOAIConstants.Domain_Customer];
                string customerSearchUrl = string.Format(CxOAIConstants.InsightsApiUrl, customerDomainAspectConfig.BaseUrl, "ch:special:all", "ch:aspect:search:azureworkloads");

                string workloadTypeDescription = ((DescriptionAttribute)Attribute.GetCustomAttribute(typeof(WorkloadTypeEnum).GetField(validWorkloadType.ToString())!, typeof(DescriptionAttribute))!)?.Description ?? validWorkloadType.ToString();

                AspectInsightsPayload customerSearchPayload = new()
                {
                    Filter = $"EntityType eq '{workloadTypeDescription}'",
                    SearchText = customerNameSearchText + "*",
                    SearchFields = new List<string>() { "EntityName" },
                    QueryType = "Full",
                    SearchMode = "All",
                    Top = 5,
                    OrderBy = new List<string>() { "EntityType desc", "Consumption desc", "EntityName asc" },
                    Select = new List<string>() { "EntityId", "EntityType", "EntityName", "SubscriptionsCount", "GroupNames" },
                    IncludeTotalResultCount = true
                };

                JObject searchResults = await CallInsightsServiceAsync(customerSearchUrl, customerDomainAspectConfig, userToken, customerSearchPayload, CxOAIConstants.Domain_Customer);

                sw.Stop();
                _logger.LogInformation("Time taken to search customers for input: '{CustomerName}' and workload type: '{WorkloadType}' was Duration: {ElapsedMs}ms",
                    customerName, workloadTypeDescription, sw.ElapsedMilliseconds);

                List<JObject> results = new();
                foreach (var searchResult in searchResults["Results"]!.ToObject<JArray>()!)
                {
                    JObject doc = searchResult["Document"]!.ToObject<JObject>()!;

                    results.Add(new JObject()
                    {
                        ["Customer Name"] = doc["EntityName"]!.ToString(),
                        ["Customer Id"] = doc["EntityId"]!.ToString(),
                        ["Workload Type"] = doc["EntityType"]!.ToString(),
                        ["Subscription Count"] = doc["SubscriptionsCount"]?.ToString(),
                        ["Programs"] = doc["GroupNames"]?.ToString(),
                        ["AgentPrompt"] = "Show Customer Name, Customer Id, Workload Type, Subscription Count and Programs."
                    });
                }

                return await BuildCustomerSearchResponseAsync(results, customerName);
            }
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ unable to search customer by name and workload type");
            _logger.LogError(ex, "Error in SearchCustomerByNameAndWorkloadType for customer: {CustomerName}", customerName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    /// <summary>
    /// Builds a standardised CXOAgentResponse for customer search results.
    /// Single result → success. Multiple → RadioButtonGroup selection. None → not-found message.
    /// </summary>
    private async Task<CXOAgentResponse> BuildCustomerSearchResponseAsync(List<JObject> results, string customerNamePrompt)
    {
        if (results.Count == 1)
        {
            var customerName = results[0]["Customer Name"]!.ToString();
            var customerId = results[0]["Customer Id"]!.ToString();
            await NotifyAsync($"✅ Customer resolved: {customerName}");
            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = $"customerName: {customerName}, {customerId}"
            };
        }

        if (results.Count > 1)
        {
            await NotifyAsync("⚠️ Multiple customers found — awaiting selection");
            return new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = true,
                NeedsInputForUser = true,
                Response = "Please select the customer from below options:",
                UIComponent = BuildRadioButtonGroup(results, "Customers", "Please select the customer from below options:", "Customer Name", "Customer Id")
            };
        }

        await NotifyAsync("❌ No customers found");
        return new CXOAgentResponse
        {
            IsSuccess = false,
            NeedsInputForUser = true,
            Response = $"No customers found for {customerNamePrompt}. Try with different name.",
        };
    }

    [Description("Retrieves the **Product Id* and other product related details provided the **Product Name**. Use this function when the user is asking for information about an Azure service or **product** or Service Group (like 'Apps', 'Azure Backup') or Azure Business portfolio (for example 'Azure Technical', 'AI Platform') . If multiple records are returned, please ask user for further selection by providing entire product details. Return the **EXACT** product Id in the format ch:product::id:{guid}.")]
    public async Task<CXOAgentResponse> SearchProductByProductName(
        [Description("The product name")] string productName)
    {
        try
        {
            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            await NotifyAsync($"🔍 Searching for product '{productName}'...");

            // TODO: Replace with ToolExecutionContext.Current.UserToken once ToolExecutionContext is implemented
            string userToken = _authContext.AccessToken!;
            _logger.LogInformation("Calling {MethodName} with parameters: {ProductName}", nameof(SearchProductByProductName), productName);
            var methodStopwatch = Stopwatch.StartNew();

            var sb = new StringBuilder();
            productName = productName.Trim();

            bool previousWasSpecialOrSpace = false;
            // Find last alphanumeric character position
            int lastAlphanumericPos = -1;
            for (int i = productName.Length - 1; i >= 0; i--)
            {
                if (char.IsLetterOrDigit(productName[i]))
                {
                    lastAlphanumericPos = i;
                    break;
                }
            }

            for (int i = 0; i < productName.Length; i++)
            {
                char c = productName[i];

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    previousWasSpecialOrSpace = false;
                }
                else // It's a space or special character
                {
                    // Only add "AND" if:
                    // 1. We haven't just added one
                    // 2. There's at least one more alphanumeric character after this position
                    if (!previousWasSpecialOrSpace && i < lastAlphanumericPos)
                    {
                        sb.Append(" AND ");
                        previousWasSpecialOrSpace = true;
                    }
                    // Skip consecutive spaces or special characters
                }
            }
            string productNameSearchText = "\\*" + sb.ToString().ToLower() + "*"; // Format the productName according to the CXO search bar formatting.

            Stopwatch sw = Stopwatch.StartNew();

            var productDomainAspectConfig = _aspectToolsConfig!.AspectsDetailsMap[CxOAIConstants.Domain_Product];
            string productSearchUrl = string.Format(CxOAIConstants.InsightsApiUrl, productDomainAspectConfig.BaseUrl, "ch:special:all", "ch:aspect:productsearch");

            AspectInsightsPayload productSearchPayload = new()
            {
                SearchText = productNameSearchText,
                SearchFields = new List<string>() { "Name" },
                QueryType = "Full",
                SearchMode = "All",
                Top = 5,
                OrderBy = new List<string>() { "Name asc" },
                Select = new List<string>() { "ServiceTreeId", "Name", "Category" },
                IncludeTotalResultCount = true
            };

            JObject searchResults = await CallInsightsServiceAsync(productSearchUrl, productDomainAspectConfig, userToken, productSearchPayload, CxOAIConstants.Domain_Product);

            sw.Stop();
            _logger.LogInformation("Time taken to search products for input: '{ProductName}' was {ElapsedMs}ms", productName, sw.ElapsedMilliseconds);

            List<JObject> results = new();
            foreach (var searchResult in searchResults["Results"]!.ToObject<JArray>()!)
            {
                JObject doc = searchResult["Document"]!.ToObject<JObject>()!;

                results.Add(new JObject()
                {
                    ["Product Name"] = doc["Name"]!.ToString(),
                    ["Product Id"] = $"EntityId: {CxOAIConstants.ChUriProductPrefix}{doc["ServiceTreeId"]!.ToString()}",
                    ["Category"] = doc["Category"]?.ToString(),
                    ["AgentPrompt"] = "Show Product Name, Product Id, Category."
                });
            }

            string productDetails = results.Count != 0 ? $"Here are the details:\n{JsonConvert.SerializeObject(results)}" : string.Empty;

            methodStopwatch.Stop();
            _logger.LogInformation("SearchProductByProductName completed - ProductName: '{ProductName}', Duration: {ElapsedMs}ms", productName, methodStopwatch.ElapsedMilliseconds);

            return await BuildProductSearchResponseAsync(results, productName);
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ unable to search product by name");
            _logger.LogError(ex, "Error in SearchProductByProductName for product: {ProductName}", productName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    /// <summary>
    /// Builds a standardised CXOAgentResponse for product search results.
    /// Single result → success. Multiple → RadioButtonGroup selection. None → not-found message.
    /// </summary>
    private async Task<CXOAgentResponse> BuildProductSearchResponseAsync(List<JObject> results, string productNamePrompt)
    {
        if (results.Count == 1)
        {
            var productName = results[0]["Product Name"]!.ToString();
            var  productId = results[0]["Product Id"]!.ToString();
            await NotifyAsync($"✅ Product resolved: {productName}");
            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = $"productName: {productName}, {productId}"
            };
        }

        if (results.Count > 1)
        {
            await NotifyAsync("⚠️ Multiple products found — awaiting selection");
            return new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = true,
                NeedsInputForUser = true,
                Response = "Please select the product from below options:",
                UIComponent = BuildRadioButtonGroup(results, "Products", "Please select the product from below options:", "Product Name", "Product Id")
            };
        }

        await NotifyAsync("❌ No products found");
        return new CXOAgentResponse
        {
            IsSuccess = false,
            NeedsInputForUser = true,
            Response = $"No products found for {productNamePrompt}. Try with different name.",
        };
    }

    #endregion

    #region Aspect Metric Config

    [Description("Searches for metric configurations matching the user's query text. Returns the top 5 matching configs with Name, Description, and Keywords. Use this when the user prompt does NOT contain an explicit Aspect Name. The caller should select the single best match from the results and pass its Name to SearchMetricConfigFilters.")]
    public async Task<CXOAgentResponse> SearchMetricConfigs(
        [Description("The user's natural language query describing the metric they want. Example: 'csat score', 'incident count', 'time to mitigate'")] string searchText,
        [Description("The resolved entity ID in CH URI format from Step 1. Example: 'ch:customer::tpid:123456', 'ch:product::id:GUID'. Used to filter results to configs that support the resolved entity type.")] string entityId)
    {
        _logger.LogInformation("SearchMetricConfigs | searchText={SearchText}, entityId={EntityId}", searchText, entityId);
        await NotifyAsync("🔍 Searching metric configurations...");

        try
        {
            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = "No search text provided. Please describe the metric you are looking for."
                };
            }

            var entityType = !string.IsNullOrWhiteSpace(entityId) ? DetermineEntityType(entityId) : null;

            var configurations = await _storeProvider.GetConfigurationsWithDescription(
                CxOAIConstants.ConfigComponent_AspectConfiguration, searchText, false);

            var results = new List<object>();
            foreach (var config in configurations)
            {
                if (config?.Configuration == null) continue;

                var contentJson = JObject.Parse(config.Configuration);
                var supportedEntities = contentJson[CxOAIConstants.Field_SupportedEntityTypes]?.ToObject<List<string>>() ?? [];

                // Filter by entity type if provided
                if (entityType != null && supportedEntities.Count > 0
                    && !supportedEntities.Any(e => string.Equals(e, entityType, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var name = contentJson.Value<string>(CxOAIConstants.Field_Name) ?? config.ConfigurationName;
                var description = contentJson.Value<string>("Description") ?? string.Empty;
                var keywords = contentJson[CxOAIConstants.Field_Keywords] is JArray kwArr
                    ? string.Join(", ", kwArr.Select(k => k.ToString()))
                    : null;

                results.Add(new
                {
                    Name = name,
                    Description = description,
                    Keywords = keywords
                });

                if (results.Count >= 5) break;
            }

            if (results.Count == 0)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = $"No metric configurations found matching '{searchText}' for entity type '{entityType ?? "any"}'."
                };
            }

            _logger.LogInformation("SearchMetricConfigs | Found {Count} matching configs for searchText={SearchText}", results.Count, searchText);
            await NotifyAsync($"✅ Found {results.Count} matching metric configs");

            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = JsonConvert.SerializeObject(results, Formatting.Indented)
            };
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ Unable to search metric configurations");
            _logger.LogError(ex, "Error in SearchMetricConfigs for searchText: {SearchText}", searchText);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                Response = $"{ex.Message}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    [Description("Fetches the full configuration details (filters, groupBy, select fields, parameters, view/unit options) for a given metric/aspect name. Call this with an explicit aspect name from the user prompt, or with the best match returned by SearchMetricConfigs. Requires the resolved entity ID from Step 1. Filters config to only those supporting the resolved entity type.")]
    public async Task<CXOAgentResponse> SearchMetricConfigFilters(
        [Description("The exact aspect configuration name. REQUIRED: must be a valid metric name like 'get_csat_score'. If the user prompt has an explicit Aspect Name (e.g., Aspect Name: 'get_csat_score'), use it directly. If the Aspect Name is missing, empty, or placeholder (e.g., Aspect Name: '', Aspect Name: [NOT FOUND], Aspect Name: NONE), do NOT call this tool — call SearchMetricConfigs first to discover the correct aspect name, then call this tool with the result.")] string aspectName,
        [Description("The resolved entity ID in CH URI format from Step 1. Example: 'ch:customer::tpid:123456', ch:customer:{guid}:id:{guid}, 'ch:product::id:GUID'. Used to filter configs by entity type.")] string entityId)
    {
        try
        {
            await NotifyAsync("📋 Loading metric configuration...");

            if (string.IsNullOrWhiteSpace(aspectName))
            {
                throw new ArgumentException($"No metric configuration specified.");
            }

            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            aspectName = aspectName.Trim();
            var entityType = !string.IsNullOrWhiteSpace(entityId) ? DetermineEntityType(entityId) : null;
            _logger.LogInformation("SearchMetricConfigFilters | Requested metric: {MetricName}, entityType: {EntityType}", aspectName, entityType ?? "none");
            var sw = Stopwatch.StartNew();

            var contentJson = await GetMetricConfigAsync(aspectName);

            if (contentJson == null)
            {
                sw.Stop();
                throw new InvalidOperationException($"No metric configuration found for '{aspectName}'. Please call SearchMetricConfigs first.");
            }

            var name = contentJson.Value<string>(CxOAIConstants.Field_Name) ?? aspectName;
            var description = contentJson.Value<string>("Description") ?? string.Empty;
            var keywords = contentJson[CxOAIConstants.Field_Keywords] is JArray kwArr
                ? string.Join(", ", kwArr.Select(k => k.ToString()))
                : null;
            var supportedEntities = contentJson[CxOAIConstants.Field_SupportedEntityTypes]?.ToObject<List<string>>() ?? [];

            // Check if the config supports the resolved entity type
            if (entityType != null && supportedEntities.Count > 0
                && !supportedEntities.Any(e => string.Equals(e, entityType, StringComparison.OrdinalIgnoreCase)))
            {
                sw.Stop();
                _logger.LogInformation("SearchMetricConfigFilters | {MetricName} does not support entity type '{EntityType}' | Duration: {ElapsedMs}ms", aspectName, entityType, sw.ElapsedMilliseconds);
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = true,
                    Response = $"Metric '{aspectName}' does not support entity type '{entityType}'. Supported entities: {string.Join(", ", supportedEntities)}."
                };
            }

            var viewOptions = GetParameterValueEnums(contentJson, CxOAIConstants.Param_View);
            var unitOptions = GetParameterValueEnums(contentJson, CxOAIConstants.Param_Unit);
            var pluginType = contentJson.Value<string>(CxOAIConstants.Field_PluginType) ?? string.Empty;
            var isEntityLess = supportedEntities.Count == 0;

            object result;
            if(isEntityLess && pluginType.Equals("PageView", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("SearchMetricConfigFilters | {MetricName} is entity-less and uses PageView plugin. Returning PageView specific response. | Duration: {ElapsedMs}ms", aspectName, sw.ElapsedMilliseconds);
                var availableFilters = GetSimplifiedFilters(contentJson, entityType);
                var availableGroupBy = GetSimplifiedGroupBy(contentJson, entityType);
                var availableSelectFields = GetSimplifiedSelectFields(contentJson);

                result = new
                {
                    Name = name,
                    Description = description,
                    Keywords = keywords,
                    SupportedEntities = supportedEntities,
                    AvailableFilters = availableFilters,
                    AvailableGroupBy = availableGroupBy,
                    AvailableSelectFields = availableSelectFields,
                    ViewOptions = viewOptions,
                    UnitOptions = unitOptions,
                    SelectionHint = $"This metric is entity-less and uses PageView plugin. Call {nameof(this.GetPageViewUrl)} with the filter above. No entity needed, DO NOT call any other tool. "
                };
            }
            if (isEntityLess)
            {
                var queryParameters = GetSimplifiedParameters(contentJson);
                result = new
                {
                    Name = name,
                    Description = description,
                    Keywords = keywords,
                    SupportedEntities = supportedEntities,
                    QueryParameters = queryParameters,
                    SelectionHint = $"Call {nameof(this.QueryByMetricConfig)} with the parameters above. No entity needed, DO NOT call any other tool. "
                };
            }
            else
            {
                var availableFilters = GetSimplifiedFilters(contentJson, entityType);
                var availableGroupBy = GetSimplifiedGroupBy(contentJson, entityType);
                var availableSelectFields = GetSimplifiedSelectFields(contentJson);

                result = new
                {
                    Name = name,
                    Description = description,
                    Keywords = keywords,
                    SupportedEntities = supportedEntities,
                    AvailableFilters = availableFilters,
                    AvailableGroupBy = availableGroupBy,
                    AvailableSelectFields = availableSelectFields,
                    ViewOptions = viewOptions,
                    UnitOptions = unitOptions,
                    SelectionHint = $"Call {nameof(this.GetMetricDataByEntityId)} with the parameters above."
                };
            }

            sw.Stop();
            _logger.LogInformation("SearchMetricConfigFilters | Parsed config for {MetricName} | EntityLess: {IsEntityLess} | pluginType: {pluginType} | Duration: {ElapsedMs}ms",
                aspectName, isEntityLess, pluginType, sw.ElapsedMilliseconds);

            await NotifyAsync("✅ Metric config loaded");

            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = JsonConvert.SerializeObject(result, Formatting.Indented)
            };
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ unable to load metric config");
            _logger.LogError(ex, "Error in SearchMetricConfigFilters for metric: {MetricName}", aspectName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    #region Metric Config Helpers

    /// <summary>
    /// Retrieves a parsed and merged metric configuration by name.
    /// Returns a cached copy if available; otherwise fetches from the configuration store,
    /// parses the JSON, merges global filters, and caches the result.
    /// </summary>
    private async Task<JObject?> GetMetricConfigAsync(string metricName)
    {
        if (_metricConfigCache.TryGetValue(metricName, out var cached))
        {
            _logger.LogInformation("GetMetricConfigAsync | Cache hit for metric: {MetricName}", metricName);
            return cached.DeepClone() as JObject;
        }

        var configs = await _storeProvider.GetConfigurationsWithDescription("AspectConfiguration", metricName, false);
        var config = configs.FirstOrDefault(c =>
            string.Equals(c.ConfigurationName, metricName, StringComparison.OrdinalIgnoreCase));

        if (config == null)
        {
            _logger.LogWarning("GetMetricConfigAsync | Config not found for metric: {MetricName}", metricName);
            return null;
        }

        var contentJson = JObject.Parse(config.Configuration);

        _metricConfigCache[metricName] = contentJson;
        _logger.LogInformation("GetMetricConfigAsync | Loaded and cached config for metric: {MetricName}", metricName);

        return contentJson.DeepClone() as JObject;
    }

    private List<object> GetSimplifiedFilters(JObject contentJson, string? entityType = null)
    {
        var filtersArray = contentJson[CxOAIConstants.Field_Filters] as JArray;
        if (filtersArray == null) return [];

        var filterConfigs = filtersArray.ToObject<List<FilterConfig>>() ?? [];

        return filterConfigs
            .Where(f => f.IsActive)
            .Where(f => entityType == null || f.SupportedEntities == null || f.SupportedEntities.Count == 0
                || f.SupportedEntities.Any(e => string.Equals(e, entityType, StringComparison.OrdinalIgnoreCase)))
            .Select(f => (object)new
            {
                f.Name,
                f.Description,
                Keywords = f.Keywords?.Count > 0 ? string.Join(", ", f.Keywords) : null,
                AllowedValues = f.ValueEnums?.Count > 0 ? f.ValueEnums : null
            })
            .ToList();
    }

    private List<object> GetSimplifiedGroupBy(JObject contentJson, string? entityType = null)
    {
        var groupByArray = contentJson[CxOAIConstants.Field_SelectGroupBy] as JArray;
        if (groupByArray == null) return [];

        var groupByConfigs = groupByArray.ToObject<List<GroupByConfig>>() ?? [];

        return groupByConfigs
            .Where(g => g.IsActive)
            .Where(g => entityType == null || g.SupportedEntities == null || g.SupportedEntities.Count == 0
                || g.SupportedEntities.Any(e => string.Equals(e, entityType, StringComparison.OrdinalIgnoreCase)))
            .Select(g => (object)new
            {
                g.Name,
                g.Description,
                Keywords = g.Keywords?.Count > 0 ? string.Join(", ", g.Keywords) : null
            })
            .ToList();
    }

    private static List<object> GetSimplifiedSelectFields(JObject contentJson)
    {
        var selectArray = contentJson[CxOAIConstants.Field_Select] as JArray;
        if (selectArray == null) return [];

        var selectConfigs = selectArray.ToObject<List<SelectFieldConfig>>() ?? [];

        return selectConfigs
            .Select(sf => (object)new
            {
                sf.DefaultFields,
                sf.Description,
                sf.Required
            })
            .ToList();
    }

    private static List<object> GetSimplifiedParameters(JObject contentJson)
    {
        var parametersArray = contentJson[CxOAIConstants.Field_Parameters] as JArray;
        if (parametersArray == null) return [];

        return parametersArray
            .OfType<JObject>()
            .Select(p => (object)new
            {
                Name = p.Value<string>("Name"),
                Type = p.Value<string>("Type"),
                Required = p.Value<bool?>("Required") ?? false,
                Default = p.Value<object>("Default"),
                Description = p.Value<string>("Description"),
                AllowedValues = p[CxOAIConstants.Field_ValueEnums]?.ToObject<List<string>>() is { Count: > 0 } enums ? enums : null
            })
            .ToList();
    }

    /// <summary>
    /// Extracts ValueEnums for a named parameter (e.g. "view", "unit") from the Parameters array.
    /// </summary>
    private static List<string>? GetParameterValueEnums(JObject contentJson, string parameterName)
    {
        var parametersArray = contentJson[CxOAIConstants.Field_Parameters] as JArray;
        if (parametersArray == null) return null;

        var param = parametersArray.OfType<JObject>()
            .FirstOrDefault(p => string.Equals(p.Value<string>("Name"), parameterName, StringComparison.OrdinalIgnoreCase));

        var enums = param?[CxOAIConstants.Field_ValueEnums]?.ToObject<List<string>>();
        return enums is { Count: > 0 } ? enums : null;
    }

    #endregion

    #endregion

    #region Data Retrieval Tool

    [Description("Retrieves metric data for a specific entity (customer, product, workload). REQUIRES calling SearchMetricConfigFilters first to discover available filters, groupBy, view, and unit options. Use EXACT metricConfigurationName from SearchMetricConfigFilters results.")]
    public async Task<CXOAgentResponse> GetMetricDataByEntityId(
        [Description("Entity Id: A unique identifier that represents an entity (such as a customer, program, workload, azure service, business portfolio or product). It is a colon-delimited string that starts with the literal 'ch:' followed by up to four segments. The format is: 'ch:{string}:{string?}:{string?}:{string}'. Here, the first and last segments ({string}) are required, while the middle two segments ({string?}) are optional. This pattern ensures that every entity ID begins with 'ch:' and then includes one to four parts separated by colons.")] string entityId,
        [Description("The EXACT metric configuration name from SearchMetricConfigFilters (e.g., 'get_csat_score', 'get_incident_count'). Case-sensitive.")] string metricConfigurationName,
        [Description("RELATIVE time range: 'last 6 months', 'last 3 months', 'last 30 days', 'last 2 weeks', 'this quarter'. Use this OR startDate+endDate, not both. Default: 'last 3 months'.")] string? timeRange = null,
        [Description("Start date (YYYY-MM-DD). For 'October 2025' use '2025-10-01'. For 'Q3 2025' use '2025-07-01'. Use with endDate.")] string? startDate = null,
        [Description("End date (YYYY-MM-DD). For 'October 2025' use '2025-10-31'. For 'Q3 2025' use '2025-09-30'. Use with startDate.")] string? endDate = null,
        [Description("View type from ViewOptions. 'Metric'/'pivotedmetric' for value/score. 'Chart'/'pivotedextendedchart' for trend/breakdown/group-by. Self Help metrics: 'pivotedmetric' for value, 'pivotedextendedchart' for trend.")] string? view = null,
        [Description("Time granularity from UnitOptions, default to 'month' if not specified. 'day' for daily, 'week' for weekly, 'month' for monthly.")] string? unit = null,
        [Description("Aggregation type from config parameters (e.g., 'count', 'percentage'). Use value from SearchMetricConfigFilters results.")] string? aggregation = null,
        [Description("Filter KEY=VALUE pairs from AvailableFilters. Use EXACT filter name. Use allowed values when provided, otherwise deduce from user prompt. Comma-separated for multiple. Example: 'Severity=A,State=Active'.")] string? filters = null,
        [Description("GroupBy field name from AvailableGroupBy. EXACT name, case-sensitive. Example: 'FullRootCausePath', 'CreationChannel', 'Severity'.")] string? groupBy = null,
        [Description("Select fields from AvailableSelectFields. Comma-separated EXACT names. Example: 'DateByUnit,CaseNumber'. Check description and view type for compatibility.")] string? selectFields = null)
    {
        _logger.LogInformation("GetMetricDataByEntityId | entityId={EntityId}, metric={Metric}, timeRange={TimeRange}, filters={Filters}, groupBy={GroupBy}",
            entityId, metricConfigurationName, timeRange ?? "(default)", filters ?? "(none)", groupBy ?? "(none)");
        await NotifyAsync("📊 Fetching metric data...");
        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Get metric config from store
            if (string.IsNullOrWhiteSpace(metricConfigurationName))
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = "No metric configuration name provided. Call SearchMetricConfigFilters first to discover available metrics."
                };
            }

            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            var metricConfig = await GetMetricConfigAsync(metricConfigurationName);

            if (metricConfig == null)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = $"Aspect config '{metricConfigurationName}' not found. Call SearchMetricConfigFilters with a valid metric configuration name."
                };
            }

            _logger.LogInformation("GetMetricDataByEntityId | Found config for metric: {MetricName}", metricConfig.Value<string>("Name"));

            // Step 2: Determine entity type
            var entityType = DetermineEntityType(entityId);

            // Step 3: Read skip guardrails from config
            var skipGuardrails = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_SkipGuardrails]?.ToObject<List<string>>() ?? [];

            // Step 4: Validate entity type
            var entityValidation = ValidateEntityType(entityType, metricConfig);
            if (!entityValidation.Success)
            {
                _logger.LogWarning("GetMetricDataByEntityId | Entity validation failed: {Error}", entityValidation.Error);
                return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser= true, Response = entityValidation.Error! };
            }

            // Step 5: Resolve date range
            DateResolutionResult dateResolution;
            if (skipGuardrails.Contains("dateRange", StringComparer.OrdinalIgnoreCase))
            {
                dateResolution = DateResolutionResult.Ok(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);
            }
            else
            {
                dateResolution = ResolveDateRange(timeRange, startDate, endDate);
                if (!dateResolution.Success)
                {
                    return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = dateResolution.Error! };
                }
            }

            // Step 6: Validate filters
            var filterValidation = ValidateFilters(filters ?? string.Empty, metricConfig, entityType);
            if (!filterValidation.Success)
            {
                return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = filterValidation.Error! };
            }
            var filterDict = filterValidation.ValidatedResult!;

            // Step 7: Validate groupBy
            var groupByValidation = ValidateGroupBy(groupBy, metricConfig, entityType);
            if (!groupByValidation.Success)
            {
                return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = groupByValidation.Error! };
            }
            var selectedGroupBy = groupByValidation.ValidatedResult!;

            // Step 8: Validate view (unless skipped)
            string? validatedView = view;
            if (!skipGuardrails.Contains("view", StringComparer.OrdinalIgnoreCase))
            {
                var viewValidation = ValidateView(view, metricConfig);
                if (!viewValidation.Success)
                    return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = viewValidation.Error! };
                validatedView = viewValidation.ValidatedResult;
            }

            // Step 9: Validate unit (unless skipped)
            string? validatedUnit = unit;
            if (!skipGuardrails.Contains("unit", StringComparer.OrdinalIgnoreCase))
            {
                var unitValidation = ValidateUnit(unit, metricConfig);
                if (!unitValidation.Success)
                    return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = unitValidation.Error! };
                validatedUnit = unitValidation.ValidatedResult;
            }

            // Step 10: Validate select fields
            List<string> selectedFields = [];
            if (!string.IsNullOrWhiteSpace(selectFields))
            {
                var selectValidation = ValidateSelectFields(selectFields, metricConfig);
                if (!selectValidation.Success)
                    return new CXOAgentResponse { IsSuccess = false, NeedsInputForUser = true, Response = selectValidation.Error! };
                selectedFields = selectValidation.ValidatedResult!;
            }

            // Step 11: Validate aggregation (if provided and not skipped)
            if (!skipGuardrails.Contains("aggregation", StringComparer.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(aggregation))
            {
                var allowedAggregations = GetParameterValueEnumsFlat(metricConfig, CxOAIConstants.Param_Aggregation);
                if (allowedAggregations.Count > 0 &&
                    !allowedAggregations.Any(a => a.Equals(aggregation, StringComparison.OrdinalIgnoreCase)))
                {
                    return new CXOAgentResponse
                    {
                        IsSuccess = false,
                        NeedsInputForUser = true,
                        Response = $"Invalid aggregation '{aggregation}'. Allowed: [{string.Join(", ", allowedAggregations)}]."
                    };
                }
            }

            // Step 12: Build ExtractedParameters
            var parameters = new ExtractedParameters
            {
                EntityId = entityId,
                EntityType = entityType,
                StartDate = dateResolution.ResolvedStartDate?.ToString("yyyy-MM-ddT00:00:00.000Z"),
                EndDate = dateResolution.ResolvedEndDate?.ToString("yyyy-MM-ddT00:00:00.000Z"),
                View = validatedView ?? GetDefaultParameterValue(metricConfig, CxOAIConstants.Param_View) ?? "Metric",
                Unit = validatedUnit ?? GetDefaultParameterValue(metricConfig, CxOAIConstants.Param_Unit) ?? "month",
                Aggregation = aggregation ?? GetDefaultParameterValue(metricConfig, CxOAIConstants.Param_Aggregation) ?? string.Empty,
                FilterValues = filterDict,
                SelectGroupByFields = selectedGroupBy,
                SelectFields = selectedFields,
                IsDateExplicitlyProvided = !string.IsNullOrWhiteSpace(timeRange) || !string.IsNullOrWhiteSpace(startDate) || !string.IsNullOrWhiteSpace(endDate)
            };

            _logger.LogInformation("GetMetricDataByEntityId | Parameters built: view={View}, unit={Unit}, dateRange={Start}..{End}, filters={FilterCount}, groupBy={GroupByCount}",
                parameters.View, parameters.Unit, parameters.StartDate, parameters.EndDate, filterDict.Count, selectedGroupBy.Count);

            // Step 13: Route to the appropriate data source handler
            var sourceType = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_SourceType]?.ToString()?.ToLowerInvariant() ?? CxOAIConstants.SourceType_Insights;

            return sourceType switch
            {
                CxOAIConstants.SourceType_Insights => await ExecuteInsightsAsync(metricConfig, parameters, entityId, entityType, sw),
                CxOAIConstants.SourceType_Cosmos => await ExecuteCosmosAsync(metricConfig, parameters, sw),
                CxOAIConstants.SourceType_Kusto => await ExecuteKustoAsync(metricConfig, parameters, sw),
                _ => throw new NotSupportedException($"Unsupported data source type: {sourceType}. Currently supported: 'insights', 'cosmos', 'kusto'.")
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            await NotifyAsync("❌ unable to fetch metric data");
            _logger.LogError(ex, "GetMetricDataByEntityId | Error for entityId={EntityId}, metric={Metric}, duration={ElapsedMs}ms",
                entityId, metricConfigurationName, sw.ElapsedMilliseconds);

            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    [Description("Retrieves metric data for cross-entity or entity-less queries (e.g., 'top 5 workloads with lowest score', 'average resolution time across all customers'). Use this ONLY when SearchMetricConfigFilters returns SupportedEntities=[] and SelectionHint mentions QueryByMetricConfig. REQUIRES calling SearchMetricConfigFilters first to discover available QueryParameters.")]
    public async Task<CXOAgentResponse> QueryByMetricConfig(
        [Description("The EXACT metric configuration name from SearchMetricConfigFilters (e.g., 'get_top_workloads_by_score'). Case-sensitive.")] string metricName,
        [Description("JSON object with query parameter key-value pairs from QueryParameters. Example: '{\"lowestOrHighest\":\"lowest\",\"topN\":\"5\"}'. Omit or pass empty to use all defaults.")] string? parameters = null)
    {
        _logger.LogInformation("QueryByMetricConfig | metricName={MetricName}, parameters={Parameters}", metricName, parameters ?? "(none)");
        await NotifyAsync("📊 Executing cross-entity query...");
        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Validate metric name
            if (string.IsNullOrWhiteSpace(metricName))
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = "No metric name provided. Call SearchMetricConfigFilters first to discover available metrics."
                };
            }

            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            // Step 2: Get metric config from cache/store
            var metricConfig = await GetMetricConfigAsync(metricName);
            if (metricConfig == null)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = $"Metric config '{metricName}' not found. Call SearchMetricConfigFilters with a valid metric name."
                };
            }

            // Step 3: Validate this is an entity-less config
            var supportedEntityTypes = metricConfig[CxOAIConstants.Field_SupportedEntityTypes]?.ToObject<List<string>>() ?? [];
            if (supportedEntityTypes.Count > 0)
            {
                var entityTypes = string.Join(", ", supportedEntityTypes);
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = true,
                    Response = $"This metric requires an entity (supported: [{entityTypes}]). Use GetMetricDataByEntityId instead."
                };
            }

            _logger.LogInformation("QueryByMetricConfig | Confirmed entity-less config for metric: {MetricName}",
                metricConfig.Value<string>(CxOAIConstants.Field_Name));

            // Step 4: Parse LLM-provided parameters JSON
            var filterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                try
                {
                    var parsedParams = JsonConvert.DeserializeObject<Dictionary<string, string>>(parameters);
                    if (parsedParams != null)
                    {
                        foreach (var kvp in parsedParams)
                        {
                            filterValues[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (JsonException)
                {
                    return new CXOAgentResponse
                    {
                        IsSuccess = false,
                        NeedsInputForUser = true,
                        Response = "Invalid parameters format. Provide a JSON object with key-value pairs, e.g.: {\"paramName\":\"value\"}"
                    };
                }
            }

            // Step 5: Validate parameter values against config and apply defaults
            var configParameters = metricConfig[CxOAIConstants.Field_Parameters] as JArray;
            if (configParameters != null)
            {
                foreach (var paramConfig in configParameters.OfType<JObject>())
                {
                    var paramName = paramConfig.Value<string>("Name");
                    if (string.IsNullOrEmpty(paramName)) continue;

                    var isRequired = paramConfig.Value<bool?>("Required") ?? false;
                    var defaultValue = paramConfig.Value<string>("Default");
                    var valueEnums = paramConfig[CxOAIConstants.Field_ValueEnums]?.ToObject<List<string>>();

                    if (filterValues.TryGetValue(paramName, out var providedValue) && !string.IsNullOrWhiteSpace(providedValue))
                    {
                        // Validate against allowed values
                        if (valueEnums != null && valueEnums.Count > 0
                            && !valueEnums.Any(v => v.Equals(providedValue, StringComparison.OrdinalIgnoreCase)))
                        {
                            return new CXOAgentResponse
                            {
                                IsSuccess = false,
                                NeedsInputForUser = true,
                                Response = $"Invalid value '{providedValue}' for parameter '{paramName}'. Allowed values: [{string.Join(", ", valueEnums)}]."
                            };
                        }
                    }
                    else if (isRequired && string.IsNullOrWhiteSpace(defaultValue))
                    {
                        // Required parameter not provided and no default
                        var allowedHint = valueEnums is { Count: > 0 }
                            ? $" Allowed values: [{string.Join(", ", valueEnums)}]."
                            : string.Empty;
                        return new CXOAgentResponse
                        {
                            IsSuccess = false,
                            NeedsInputForUser = true,
                            Response = $"Required parameter '{paramName}' is missing.{allowedHint}"
                        };
                    }
                    else if (!filterValues.ContainsKey(paramName) && !string.IsNullOrWhiteSpace(defaultValue))
                    {
                        // Apply default value
                        filterValues[paramName] = defaultValue;
                    }
                }
            }

            _logger.LogInformation("QueryByMetricConfig | Parameters validated: {Parameters}",
                JsonConvert.SerializeObject(filterValues));

            // Step 6: Build ExtractedParameters with entity-less defaults
            var extractedParams = new ExtractedParameters
            {
                EntityId = string.Empty,
                EntityType = string.Empty,
                FilterValues = filterValues,
                View = string.Empty,
                Unit = string.Empty,
                Aggregation = string.Empty,
                IsDateExplicitlyProvided = false
            };

            // Step 7: Route to the appropriate data source handler
            var sourceType = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_SourceType]?.ToString()?.ToLowerInvariant()
                ?? CxOAIConstants.SourceType_Insights;

            _logger.LogInformation("QueryByMetricConfig | Routing to data source: {SourceType}", sourceType);

            return sourceType switch
            {
                CxOAIConstants.SourceType_Insights => await ExecuteInsightsAsync(metricConfig, extractedParams, string.Empty, string.Empty, sw),
                CxOAIConstants.SourceType_Cosmos => await ExecuteCosmosAsync(metricConfig, extractedParams, sw),
                CxOAIConstants.SourceType_Kusto => await ExecuteKustoAsync(metricConfig, extractedParams, sw),
                _ => throw new NotSupportedException($"Unsupported data source type: {sourceType}. Currently supported: 'insights', 'cosmos', 'kusto'.")
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            await NotifyAsync("❌ Unable to execute cross-entity query");
            _logger.LogError(ex, "QueryByMetricConfig | Error for metric={MetricName}, duration={ElapsedMs}ms",
                metricName, sw.ElapsedMilliseconds);

            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    /// <summary>
    /// Resolves the domain key from metric config's Domain field.
    /// </summary>
    private static string GetDomainKey(JObject metricConfig)
    {
        var domain = metricConfig.Value<string>("Domain")?.ToLowerInvariant() ?? CxOAIConstants.Domain_Customer;
        return domain switch
        {
            "support" => CxOAIConstants.Domain_Support,
            "customer" => CxOAIConstants.Domain_Customer,
            "product" => CxOAIConstants.Domain_Product,
            _ => domain
        };
    }

    #region Data Retrieval Tool Helpers

    #region Date Resolution & Entity Type

    /// <summary>
    /// Resolves date range from either explicit dates (YYYY-MM-DD) or relative time range string.
    /// Returns a DateResolutionResult with success/failure and resolved dates.
    /// </summary>
    internal static DateResolutionResult ResolveDateRange(string? timeRange, string? startDate, string? endDate)
    {
        // Explicit dates (when no timeRange but start+end provided)
        if (string.IsNullOrWhiteSpace(timeRange)
            && !string.IsNullOrWhiteSpace(startDate)
            && !string.IsNullOrWhiteSpace(endDate))
        {
            if (!DateTime.TryParseExact(startDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var start))
            {
                return DateResolutionResult.Fail($"Invalid startDate format '{startDate}'. Use YYYY-MM-DD.");
            }

            if (!DateTime.TryParseExact(endDate, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var end))
            {
                return DateResolutionResult.Fail($"Invalid endDate format '{endDate}'. Use YYYY-MM-DD.");
            }

            if (start > end)
            {
                return DateResolutionResult.Fail("startDate cannot be after endDate.");
            }

            return DateResolutionResult.Ok(start, end);
        }

        // Relative time range (or default)
        var (resolvedStart, resolvedEnd) = CalculateDateRangeInternal(timeRange);
        return DateResolutionResult.Ok(resolvedStart, resolvedEnd);
    }

    /// <summary>
    /// Parses relative time range strings: "last N months/days/weeks", "this quarter", "last year".
    /// Defaults to last 6 months when null/empty.
    /// </summary>
    internal static (DateTime startDate, DateTime endDate) CalculateDateRangeInternal(string? timeRange)
    {
        var endDate = DateTime.UtcNow.Date;

        if (string.IsNullOrWhiteSpace(timeRange))
            return (endDate.AddMonths(-6), endDate);

        var desc = timeRange.ToLowerInvariant();

        if (desc.Contains("month"))
        {
            var months = ExtractNumber(desc, 1);
            return (endDate.AddMonths(-months), endDate);
        }
        if (desc.Contains("day"))
        {
            var days = ExtractNumber(desc, 30);
            return (endDate.AddDays(-days), endDate);
        }
        if (desc.Contains("week"))
        {
            var weeks = ExtractNumber(desc, 1);
            return (endDate.AddDays(-weeks * 7), endDate);
        }
        if (desc.Contains("quarter"))
            return (endDate.AddMonths(-3), endDate);
        if (desc.Contains("year"))
        {
            var years = ExtractNumber(desc, 1);
            return (endDate.AddYears(-years), endDate);
        }

        // Fallback: last 30 days
        return (endDate.AddDays(-30), endDate);
    }

    /// <summary>
    /// Extracts first integer from a string (e.g., "last 6 months" → 6). Returns defaultValue if none found.
    /// </summary>
    private static int ExtractNumber(string text, int defaultValue)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        return match.Success && int.TryParse(match.Value, out var num) ? num : defaultValue;
    }

    /// <summary>
    /// Determines entity type from CH URI prefix.
    /// ch:customer::tpid: → "customer", ch:product: → "product", ch:group: → "program"
    /// </summary>
    internal static string DetermineEntityType(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return "unknown";

        if (entityId.StartsWith("ch:customer::tpid:", StringComparison.OrdinalIgnoreCase))
            return "customer";
        if (entityId.StartsWith("ch:customer:", StringComparison.OrdinalIgnoreCase))
            return "workload";
        if (entityId.StartsWith("ch:product:", StringComparison.OrdinalIgnoreCase))
            return "product";
        if (entityId.StartsWith("ch:group:", StringComparison.OrdinalIgnoreCase))
            return "program";

        return "unknown";
    }

    /// <summary>
    /// Gets the default value for a named parameter (e.g., "aggregation", "view", "unit")
    /// from the Parameters array in the metric config.
    /// </summary>
    internal static string? GetDefaultParameterValue(JObject metricConfig, string parameterName)
    {
        var parametersArray = metricConfig[CxOAIConstants.Field_Parameters] as JArray;
        if (parametersArray == null) return null;

        var param = parametersArray.OfType<JObject>()
            .FirstOrDefault(p => string.Equals(p.Value<string>(CxOAIConstants.Field_Name), parameterName, StringComparison.OrdinalIgnoreCase));

        return param?.Value<string>("Default");
    }

    #endregion

    #region Validation Helpers (Inline Guardrails)

    /// <summary>
    /// Validates entity type against the metric config's SupportedEntityTypes.
    /// If SupportedEntityTypes is null/empty, all entity types are allowed.
    /// </summary>
    internal static ValidationResult<string> ValidateEntityType(string entityType, JObject metricConfig)
    {
        var supportedEntityTypes = metricConfig[CxOAIConstants.Field_SupportedEntityTypes] as JArray;

        // Null or empty means all entity types are valid
        if (supportedEntityTypes == null || !supportedEntityTypes.Any())
            return ValidationResult<string>.Ok("Valid entity type selection.");

        if (supportedEntityTypes.Any(jt => string.Equals(jt.ToString().Trim(), entityType, StringComparison.OrdinalIgnoreCase)))
            return ValidationResult<string>.Ok("Valid entity type selection.");

        var available = string.Join(", ", supportedEntityTypes.Select(jt => jt.ToString()));
        return ValidationResult<string>.Fail(
            $"The selected entity type '{entityType}' is not valid for the requested metric. " +
            $"Valid entity types are: [{available}]. Present these options to the user and wait for their selection.");
    }

    /// <summary>
    /// Parses "KEY=VALUE" filter pairs (separated by ; or ,), validates each filter name exists
    /// in the config, is active, supports the entity type, and has an allowed value.
    /// </summary>
    internal static ValidationResult<Dictionary<string, string>> ValidateFilters(
        string filters, JObject metricConfig, string entityType)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(filters))
            return ValidationResult<Dictionary<string, string>>.Ok(result);

        var filtersArray = metricConfig[CxOAIConstants.Field_Filters] as JArray;
        if (filtersArray == null)
            return ValidationResult<Dictionary<string, string>>.Ok(result);

        var filterConfigs = filtersArray.ToObject<List<FilterConfig>>()?
            .Where(f => f.IsActive).ToList() ?? [];

        var separator = filters.Contains(';') ? ';' : ',';
        var pairs = filters.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                return ValidationResult<Dictionary<string, string>>.Fail(
                    $"Invalid filter format: '{pair}'. Use KEY=VALUE format.");
            }

            var filterName = parts[0].Trim();
            var filterValue = parts[1].Trim();

            var filterConfig = filterConfigs.FirstOrDefault(f =>
                f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

            if (filterConfig == null)
            {
                var available = string.Join(", ", filterConfigs.Select(f => f.Name));
                return ValidationResult<Dictionary<string, string>>.Fail(
                    $"Unknown filter '{filterName}'. Available filters: [{available}].");
            }

            // Check filter supports this entity type
            if (filterConfig.SupportedEntities?.Count > 0 &&
                !filterConfig.SupportedEntities.Any(e => e.Equals(entityType, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult<Dictionary<string, string>>.Fail(
                    $"Filter '{filterName}' is not supported for entity type '{entityType}'. " +
                    $"Supported: [{string.Join(", ", filterConfig.SupportedEntities)}].");
            }

            // Check value is allowed (if enum defined)
            if (filterConfig.ValueEnums?.Count > 0 &&
                !filterConfig.ValueEnums.Any(v => v.Equals(filterValue, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult<Dictionary<string, string>>.Fail(
                    $"Invalid value '{filterValue}' for filter '{filterName}'. " +
                    $"Allowed: [{string.Join(", ", filterConfig.ValueEnums)}].");
            }

            result[filterName] = filterValue;
        }

        return ValidationResult<Dictionary<string, string>>.Ok(result);
    }

    /// <summary>
    /// Validates groupBy field(s) against the metric config's SelectGroupBy, checking active status
    /// and entity type support.
    /// </summary>
    internal static ValidationResult<List<string>> ValidateGroupBy(
        string? groupBy, JObject metricConfig, string entityType)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
            return ValidationResult<List<string>>.Ok([]);

        var selectGroupByArray = metricConfig[CxOAIConstants.Field_SelectGroupBy] as JArray;
        if (selectGroupByArray == null)
            return ValidationResult<List<string>>.Ok([]);

        var groupByConfigs = selectGroupByArray.ToObject<List<GroupByConfig>>()?
            .Where(g => g.IsActive).ToList() ?? [];

        var separator = groupBy.Contains(';') ? ';' : ',';
        var groupByList = groupBy.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim()).ToList();

        foreach (var field in groupByList)
        {
            var config = groupByConfigs.FirstOrDefault(g =>
                g.Name.Equals(field, StringComparison.OrdinalIgnoreCase));

            if (config == null)
            {
                var available = string.Join(", ", groupByConfigs.Select(g => g.Name));
                return ValidationResult<List<string>>.Fail(
                    $"Invalid groupBy field '{field}'. Available: [{available}].");
            }

            if (config.SupportedEntities?.Count > 0 &&
                !config.SupportedEntities.Any(e => e.Equals(entityType, StringComparison.OrdinalIgnoreCase)))
            {
                return ValidationResult<List<string>>.Fail(
                    $"GroupBy '{field}' is not supported for entity type '{entityType}'. " +
                    $"Supported: [{string.Join(", ", config.SupportedEntities)}].");
            }
        }

        return ValidationResult<List<string>>.Ok(groupByList);
    }

    /// <summary>
    /// Validates view parameter against config's allowed view ValueEnums.
    /// Defaults to "Metric" if not provided.
    /// </summary>
    internal static ValidationResult<string> ValidateView(string? view, JObject metricConfig)
    {
        var allowedViews = GetParameterValueEnumsFlat(metricConfig, CxOAIConstants.Param_View);
        var defaultView = GetDefaultParameterValue(metricConfig, CxOAIConstants.Param_View) ?? "Metric";

        view = string.IsNullOrWhiteSpace(view) ? defaultView : view;

        if (allowedViews.Count > 0 &&
            !allowedViews.Any(v => v.Equals(view, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationResult<string>.Fail(
                $"Invalid view '{view}'. Available: [{string.Join(", ", allowedViews)}].");
        }

        return ValidationResult<string>.Ok(view);
    }

    /// <summary>
    /// Validates unit parameter against config's allowed unit ValueEnums.
    /// Defaults to "month" if not provided. Falls back to ["day","week","month"] if no enums configured.
    /// </summary>
    internal static ValidationResult<string> ValidateUnit(string? unit, JObject metricConfig)
    {
        var allowedUnits = GetParameterValueEnumsFlat(metricConfig, CxOAIConstants.Param_Unit);
        if (allowedUnits.Count == 0)
            allowedUnits = ["day", "week", "month"];

        var defaultUnit = GetDefaultParameterValue(metricConfig, CxOAIConstants.Param_Unit) ?? "month";

        unit = string.IsNullOrWhiteSpace(unit) ? defaultUnit : unit;

        if (!allowedUnits.Any(u => u.Equals(unit, StringComparison.OrdinalIgnoreCase)))
        {
            return ValidationResult<string>.Fail(
                $"Invalid unit '{unit}'. Available: [{string.Join(", ", allowedUnits)}].");
        }

        return ValidationResult<string>.Ok(unit);
    }

    /// <summary>
    /// Validates select fields against config's Select[].DefaultFields.
    /// </summary>
    internal static ValidationResult<List<string>> ValidateSelectFields(
        string? selectFields, JObject metricConfig)
    {
        if (string.IsNullOrWhiteSpace(selectFields))
            return ValidationResult<List<string>>.Ok([]);

        var selectArray = metricConfig[CxOAIConstants.Field_Select] as JArray;
        if (selectArray == null)
            return ValidationResult<List<string>>.Ok([]);

        var selectConfigs = selectArray.ToObject<List<SelectFieldConfig>>() ?? [];
        var selectConfig = selectConfigs.FirstOrDefault();
        if (selectConfig == null)
            return ValidationResult<List<string>>.Ok([]);

        var separator = selectFields.Contains(';') ? ';' : ',';
        var fieldList = selectFields.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim()).ToList();

        var defaultFields = selectConfig.DefaultFields ?? [];
        var invalidFields = fieldList
            .Where(f => !defaultFields.Any(df => df.Equals(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (invalidFields.Count > 0)
        {
            return ValidationResult<List<string>>.Fail(
                $"Invalid select field(s): [{string.Join(", ", invalidFields)}]. " +
                $"Available: [{string.Join(", ", defaultFields)}]." +
                (!string.IsNullOrWhiteSpace(selectConfig.Description) ? $" {selectConfig.Description}" : ""));
        }

        return ValidationResult<List<string>>.Ok(fieldList);
    }

    /// <summary>
    /// Gets ValueEnums for a named parameter as a flat List&lt;string&gt;.
    /// </summary>
    private static List<string> GetParameterValueEnumsFlat(JObject metricConfig, string parameterName)
    {
        var parametersArray = metricConfig[CxOAIConstants.Field_Parameters] as JArray;
        if (parametersArray == null) return [];

        var param = parametersArray.OfType<JObject>()
            .FirstOrDefault(p => string.Equals(p.Value<string>(CxOAIConstants.Field_Name), parameterName, StringComparison.OrdinalIgnoreCase));

        return param?[CxOAIConstants.Field_ValueEnums]?.ToObject<List<string>>() ?? [];
    }

    #endregion

    #endregion

    #endregion

    #region Insights Data Source

    /// <summary>
    /// Executes the Insights (Aspect) data source: resolves domain, builds URL/payload, calls API, postprocesses response.
    /// </summary>
    private async Task<CXOAgentResponse> ExecuteInsightsAsync(
        JObject metricConfig, ExtractedParameters parameters,
        string entityId, string entityType, Stopwatch sw)
    {
        // Resolve domain config
        var domainKey = GetDomainKey(metricConfig);
        var aspectsMap = _aspectToolsConfig?.AspectsDetailsMap;
        if (aspectsMap == null || !aspectsMap.TryGetValue(domainKey, out var domainConfig))
        {
            throw new InvalidOperationException($"Insights API configuration not found for domain: {domainKey}. Ensure AspectToolsConfig is loaded.");
        }

        // Build URL and payload
        var apiUrl = BuildAspectUrl(metricConfig, parameters);
        var payload = BuildPayload(metricConfig, parameters);
        var fullUrl = $"{domainConfig.BaseUrl}{apiUrl}";

        _logger.LogInformation("ExecuteInsightsAsync | Calling API: {Url} | Payload: {Payload}",
            fullUrl, JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

        // Call Insights API
        await NotifyAsync("🔗 Calling Insights API...");
        JObject apiResponse = await CallInsightsServiceAsync(fullUrl, domainConfig, _authContext.AccessToken!, payload);

        // Postprocess
        await NotifyAsync("⚙️ Processing response...");
        var postprocessedContent = await PostprocessInsightsResponseAsync(apiResponse, parameters.View, metricConfig, parameters);

        sw.Stop();
        _logger.LogInformation("ExecuteInsightsAsync | Success: metric={Metric}, duration={ElapsedMs}ms",
            metricConfig.Value<string>(CxOAIConstants.Field_Name), sw.ElapsedMilliseconds);

        await NotifyAsync("✅ Data ready");

        return new CXOAgentResponse
        {
            IsSuccess = true,
            Response = postprocessedContent
        };
    }

    /// <summary>
    /// Calls the Insights Service API with OBO token acquisition.
    /// Acquires an OBO token using MSAL ConfidentialClient, then POSTs the payload.
    /// </summary>
    internal async Task<JObject> CallInsightsServiceAsync(
        string apiUrl,
        AspectApiConfig domainConfig,
        string userToken,
        AspectInsightsPayload payload,
        string domainName = "")
    {
        var tokenConfig = domainConfig.TokenAcquisitionConfig
            ?? throw new InvalidOperationException($"TokenAcquisitionConfig is null for domain '{domainName}'.");

        // Acquire OBO token
        var rawToken = userToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? userToken["Bearer ".Length..]
            : userToken;

        var clientApp = Microsoft.Identity.Client.ConfidentialClientApplicationBuilder
            .Create(tokenConfig.AppClientId)
            .WithTenantId(tokenConfig.TenantId)
            .WithCertificate(tokenConfig.Certificate)
            .Build();

        var authResult = await clientApp
            .AcquireTokenOnBehalfOf(tokenConfig.Scopes, new Microsoft.Identity.Client.UserAssertion(rawToken))
            .WithSendX5C(true)
            .ExecuteAsync();

        // POST with acquired token
        var requestContent = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        request.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("CallInsightsServiceAsync | {Domain} API returned {StatusCode}: {Body}", domainName, response.StatusCode, responseBody);
            throw new HttpRequestException($"Insights API returned {response.StatusCode}: {responseBody}");
        }

        // Auto-wrap JSON arrays into { "items": [...] }
        var trimmed = responseBody.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return new JObject { ["items"] = JArray.Parse(responseBody) };
        }

        return JObject.Parse(responseBody);
    }

    /// <summary>
    /// Post-processes an Insights API response based on view type.
    /// Formats raw API data into a string suitable for LLM consumption.
    /// Appends a CxObserve navigation link when available.
    /// </summary>
    internal async Task<string> PostprocessInsightsResponseAsync(
        JObject apiResult, string viewType, JObject metricConfig, ExtractedParameters extractedParameters)
    {
        var metricName = metricConfig[CxOAIConstants.Field_Name]?.ToString() ?? string.Empty;
        var normalizedView = NormalizeViewType(viewType);

        string navigationLink;
        try
        {
            navigationLink = await GetNavigationUrlAsync(metricConfig, extractedParameters);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate navigation URL during post-processing, continuing without link");
            navigationLink = string.Empty;
        }

        var content = ProcessWithCodeDefault(apiResult, normalizedView, metricName, navigationLink, extractedParameters);

        // Prepend AgentPrompt if configured
        var agentPrompt = metricConfig[CxOAIConstants.Field_AdditionalMetadata]?[CxOAIConstants.Field_AgentPrompt]?.ToString();
        if (!string.IsNullOrWhiteSpace(agentPrompt))
        {
            content = $"<SYSTEM_NOTE>{agentPrompt}</SYSTEM_NOTE>\n\n{content}";
        }

        return content;
    }

    /// <summary>
    /// Normalizes view type strings to canonical form: metric, chart, or list.
    /// </summary>
    private static string NormalizeViewType(string viewType)
    {
        return viewType.ToLowerInvariant() switch
        {
            "chart" or "trend" or "pivotedextendedchart" or "pivotedchart" => "chart",
            "list" => "list",
            "metric" or "" or "pivotedmetric" => "metric",
            var other => other
        };
    }

    /// <summary>
    /// Formats Insights API response by view type using code-default logic.
    /// Metric: extracts Value. Chart: iterates items[] (grouped when SelectGroupByFields is non-empty). List: wraps with nav link.
    /// </summary>
    private string ProcessWithCodeDefault(JObject apiResult, string viewType, string metricName, string? navigationLink, ExtractedParameters extractedParameters)
    {
        var linkSuffix = !string.IsNullOrEmpty(navigationLink)
            ? $" Check <a target=\"_blank\" href=\"{navigationLink}\">this</a> for more details."
            : "";

        if (viewType == "chart")
        {
            JArray? itemsArray = (JArray?)apiResult["items"];
            if (itemsArray == null || !itemsArray.Any())
                return $"No trend data available for {metricName}.{linkSuffix}";

            bool isGrouped = extractedParameters.SelectGroupByFields != null
                && extractedParameters.SelectGroupByFields.Count > 0;

            if (!isGrouped)
            {
                foreach (JObject item in itemsArray)
                {
                    var valuesArray = (JArray?)item["Values"];
                    if (valuesArray != null && valuesArray.Count > 1)
                    {
                        isGrouped = true;
                        break;
                    }
                }
            }

            if (isGrouped)
            {
                var results = new List<object>();
                foreach (JObject item in itemsArray)
                {
                    string timeLabel = item["Label"]?.ToString() ?? "";
                    var valuesArray = item["Values"] as JArray;
                    if (valuesArray == null) continue;

                    foreach (JObject valueItem in valuesArray)
                    {
                        results.Add(new
                        {
                            timeLabel,
                            seriesLabel = valueItem["Label"]?.ToString() ?? "",
                            value = valueItem["Value"]?.Value<double?>()
                        });
                    }
                }
                return $"Here is the grouped trend data in JSON format: {JsonConvert.SerializeObject(results)}.{linkSuffix}";
            }
            else
            {
                var results = new List<object>();
                foreach (JObject item in itemsArray)
                {
                    string itemLabel = item["Label"]?.ToString() ?? "";
                    var valuesArray = item["Values"] as JArray;
                    if (valuesArray == null) continue;

                    foreach (JObject valueItem in valuesArray)
                    {
                        results.Add(new
                        {
                            label = itemLabel,
                            value = valueItem["Value"]?.Value<double?>()
                        });
                    }
                }
                return $"Here is the trend data in JSON format: {JsonConvert.SerializeObject(results)}.{linkSuffix}";
            }
        }
        else if (viewType == "list")
        {
            JObject result = new()
            {
                ["NavigationLink"] = navigationLink ?? "",
                ["InsightsAPIResponse"] = apiResult
            };
            return JsonConvert.SerializeObject(result);
        }
        else
        {
            // Metric (default)
            var metricValue = apiResult.SelectToken("Value")?.ToString() ?? "N/A";
            return $"The {metricName} for the customer is {metricValue}.{linkSuffix}";
        }
    }

    #region URL & Payload Building

    /// <summary>
    /// Builds the full API path by replacing placeholders in the config's SystemUrl template.
    /// Template placeholders: {startDate}, {endDate}, {view}, {unit}, {aggregation}
    /// Final path: /api/Insights/{entityUri}/aspects/{aspectUrl}
    /// </summary>
    internal string BuildAspectUrl(JObject metricConfig, ExtractedParameters parameters)
    {
        var aspectUrl = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_SystemUrl]?.ToString() ?? string.Empty;

        aspectUrl = ReplacePlaceholder(aspectUrl, "{startDate}", parameters.StartDate);
        aspectUrl = ReplacePlaceholder(aspectUrl, "{endDate}", parameters.EndDate);
        aspectUrl = ReplacePlaceholder(aspectUrl, "{view}", parameters.View);
        aspectUrl = ReplacePlaceholder(aspectUrl, "{unit}", parameters.Unit);
        aspectUrl = ReplacePlaceholder(aspectUrl, "{aggregation}", parameters.Aggregation);

        var entityUri = !string.IsNullOrEmpty(parameters.EntityId)
            ? parameters.EntityId
            : "ch:special:all";

        var fullApiPath = string.Format(CxOAIConstants.InsightsApiPathFormat, entityUri, aspectUrl);
        _logger.LogInformation("BuildAspectUrl | Entity: {EntityUri} | Path: {Path}", entityUri, fullApiPath);

        return fullApiPath;
    }

    private static string ReplacePlaceholder(string url, string placeholder, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return url;
        return url.Replace(placeholder, value);
    }

    /// <summary>
    /// Builds the request payload: deserializes PayloadFormat template, merges filter expression,
    /// global filter expressions, and select fields.
    /// </summary>
    internal AspectInsightsPayload BuildPayload(JObject metricConfig, ExtractedParameters parameters)
    {
        var payloadFormat = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_PayloadFormat];
        var payload = payloadFormat != null
            ? JsonConvert.DeserializeObject<AspectInsightsPayload>(payloadFormat.ToString())!
            : new AspectInsightsPayload();

        // Build filter expression from config + user-provided filter values
        var filter = BuildFilterExpression(metricConfig, parameters);

        // Merge filter into payload (payload may have a base filter from template)
        if (!string.IsNullOrWhiteSpace(filter) && !string.IsNullOrWhiteSpace(payload.Filter?.Trim()))
        {
            payload.Filter = payload.Filter + " and " + filter;
        }
        else
        {
            payload.Filter = !string.IsNullOrWhiteSpace(payload.Filter) ? payload.Filter : filter;
        }

        // Build select fields
        payload.Select = BuildSelectFields(metricConfig, parameters, payload.Select);

        _logger.LogInformation("BuildPayload | Filter: {Filter} | Select: {Select}",
            payload.Filter ?? "(none)",
            payload.Select != null ? string.Join(", ", payload.Select) : "(none)");

        return payload;
    }

    /// <summary>
    /// Iterates active filter configs, applies expression templates with {value} replacement.
    /// Uses user-provided values or config defaults for required filters.
    /// </summary>
    internal string BuildFilterExpression(JObject metricConfig, ExtractedParameters parameters)
    {
        var filterExpressions = new List<string>();

        var filtersArray = metricConfig[CxOAIConstants.Field_Filters] as JArray;
        if (filtersArray == null || !filtersArray.Any())
        {
            return string.Empty;
        }

        var filterConfigs = filtersArray.ToObject<List<FilterConfig>>()?
            .Where(f => f.IsActive).ToList() ?? [];

        foreach (var filterConfig in filterConfigs)
        {
            string? filterValue = null;

            if (parameters.FilterValues.TryGetValue(filterConfig.Name, out var paramValue))
            {
                filterValue = paramValue;
            }
            else if (filterConfig.Required || !string.IsNullOrEmpty(filterConfig.Default))
            {
                filterValue = filterConfig.Default;
            }

            if (string.IsNullOrEmpty(filterValue))
                continue;

            string expressionTemplate = filterConfig.Expression;
            if (!string.IsNullOrEmpty(expressionTemplate))
            {
                var expression = expressionTemplate.Replace("{value}", filterValue);
                filterExpressions.Add(expression);
                _logger.LogInformation("BuildFilterExpression | {FilterName} = {Expression}", filterConfig.Name, expression);
            }
        }

        return string.Join(" and ", filterExpressions);
    }

    /// <summary>
    /// Builds select fields: user-provided → config defaults → insert groupBy fields → reorder DateByUnit to first.
    /// </summary>
    internal IList<string> BuildSelectFields(
        JObject metricConfig,
        ExtractedParameters parameters,
        IList<string>? payloadSelectFields)
    {
        IList<string> selectFields = parameters.SelectFields?.Count > 0
            ? parameters.SelectFields
            : new List<string>();

        // If no user-provided select fields, use defaults from config
        if (selectFields.Count == 0)
        {
            payloadSelectFields ??= new List<string>();
            var selectArray = metricConfig[CxOAIConstants.Field_Select] as JArray;
            if (selectArray?.Any() == true)
            {
                var configSelects = selectArray.ToObject<List<SelectFieldConfig>>();
                if (configSelects != null)
                {
                    foreach (var field in configSelects.SelectMany(f => f.DefaultFields))
                    {
                        payloadSelectFields.Add(field);
                    }
                }
                selectFields = payloadSelectFields.Distinct().ToList();
            }
        }

        // Insert groupBy fields before the last metric column
        foreach (var groupByField in parameters.SelectGroupByFields)
        {
            if (selectFields.Count > 1)
            {
                selectFields.Insert(selectFields.Count - 1, groupByField);
            }
            else
            {
                selectFields.Add(groupByField);
            }
        }

        // Move DateByUnit to first position if present
        int dateByUnitIndex = selectFields.IndexOf("DateByUnit");
        if (dateByUnitIndex > 0)
        {
            selectFields.RemoveAt(dateByUnitIndex);
            selectFields.Insert(0, "DateByUnit");
        }

        return selectFields;
    }

    #endregion

    #endregion

    #region Cosmos Data Source

    /// <summary>
    /// Executes the Cosmos data source: reads config, builds query, queries Cosmos DB, postprocesses response.
    /// </summary>
    private async Task<CXOAgentResponse> ExecuteCosmosAsync(
        JObject metricConfig, ExtractedParameters parameters, Stopwatch sw)
    {
        var metricName = metricConfig.Value<string>(CxOAIConstants.Field_Name) ?? string.Empty;

        var (queryTemplate, maxItemCount, cosmosDbConfig, connectionKey) = ValidateCosmosConfig(metricConfig, metricName);

        // Build query with parameter substitution
        var configParameters = metricConfig.SelectToken(CxOAIConstants.Field_Parameters);
        var resolvedQuery = BuildCosmosQuery(queryTemplate, configParameters, parameters);

        _logger.LogInformation("ExecuteCosmosAsync | Executing query for metric {Metric}, connectionKey={Key}: {Query}",
            metricName, connectionKey, resolvedQuery);

        // Execute Cosmos DB query
        await NotifyAsync("🔗 Querying data store...");
        var queryStopwatch = Stopwatch.StartNew();
        List<JObject> results;
        try
        {
            results = await QueryCosmosDbAsync(cosmosDbConfig, resolvedQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteCosmosAsync | Cosmos query failed for metric {Metric}", metricName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                Response = $"Error executing Cosmos DB query for metric '{metricName}': {ex.Message}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
        queryStopwatch.Stop();

        // Limit results to MaxItemCount
        var limitedResults = results.Take(maxItemCount).ToList();
        _logger.LogInformation("ExecuteCosmosAsync | Query returned {Count} results in {ElapsedMs}ms (limited to {Max})",
            results.Count, queryStopwatch.ElapsedMilliseconds, maxItemCount);

        if (limitedResults.Count == 0)
        {
            sw.Stop();
            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = $"No data found for metric '{metricName}'."
            };
        }

        // Postprocess
        await NotifyAsync("⚙️ Processing response...");
        var content = await PostprocessCosmosResponseAsync(limitedResults, metricConfig, parameters, metricName);

        sw.Stop();
        _logger.LogInformation("ExecuteCosmosAsync | Success: metric={Metric}, results={Count}, duration={ElapsedMs}ms",
            metricName, limitedResults.Count, sw.ElapsedMilliseconds);

        await NotifyAsync("✅ Data ready");

        return new CXOAgentResponse
        {
            IsSuccess = true,
            Response = content
        };
    }

    /// <summary>
    /// Validates and resolves Cosmos DB configuration from the metric config and AspectToolsConfig.
    /// Throws InvalidOperationException if any required configuration is missing.
    /// </summary>
    private (string QueryTemplate, int MaxItemCount, AspectCosmosDbConfig CosmosDbConfig, string ConnectionKey) ValidateCosmosConfig(
        JObject metricConfig, string metricName)
    {
        var cosmosConfig = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_Cosmos] as JObject
            ?? throw new InvalidOperationException($"Cosmos configuration is missing in DataSource for metric '{metricName}'.");

        var connectionKey = cosmosConfig.Value<string>(CxOAIConstants.Field_ConnectionKey) ?? string.Empty;
        var queryTemplate = cosmosConfig.Value<string>(CxOAIConstants.Field_Query) ?? string.Empty;
        var maxItemCount = cosmosConfig.Value<int?>(CxOAIConstants.Field_MaxItemCount) ?? 10;

        if (string.IsNullOrWhiteSpace(connectionKey))
            throw new InvalidOperationException($"ConnectionKey is missing in Cosmos config for metric '{metricName}'.");

        if (string.IsNullOrWhiteSpace(queryTemplate))
            throw new InvalidOperationException($"Query is missing in Cosmos config for metric '{metricName}'.");

        var cosmosDbsMaps = _aspectToolsConfig?.CosmosDbsMaps;
        if (cosmosDbsMaps == null || !cosmosDbsMaps.TryGetValue(connectionKey, out var cosmosDbConfig))
            throw new InvalidOperationException($"No Cosmos DB connection configured for key '{connectionKey}' in metric '{metricName}'. Ensure the key exists in cosmosDbsMaps configuration.");

        if (string.IsNullOrWhiteSpace(cosmosDbConfig.AccountEndpoint))
            throw new InvalidOperationException($"AccountEndpoint is missing for Cosmos DB connection '{connectionKey}'.");

        return (queryTemplate, maxItemCount, cosmosDbConfig, connectionKey);
    }

    /// <summary>
    /// Queries Cosmos DB using the SDK directly. Uses ManagedIdentityCredential (release) or VisualStudioCredential (debug).
    /// </summary>
    private static async Task<List<JObject>> QueryCosmosDbAsync(AspectCosmosDbConfig config, QueryDefinition queryDefinition)
    {
#if DEBUG
        var credential = new Azure.Identity.VisualStudioCredential();
#else
        var credential = new Azure.Identity.ManagedIdentityCredential();
#endif

        using var cosmosClient = new CosmosClient(config.AccountEndpoint, credential);
        var container = cosmosClient.GetContainer(config.DatabaseId, config.ContainerId);
        var iterator = container.GetItemQueryIterator<JObject>(queryDefinition);

        var results = new List<JObject>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                results.Add(item);
            }
        }

        return results;
    }

    /// <summary>
    /// Builds the Cosmos query by substituting placeholders: {entityId}, {startDate}, {endDate}, and config-driven parameters.
    /// Entity ID is stripped from CH URI format (e.g., 'ch:customer::tpid:123456' → '123456').
    /// </summary>
    internal static QueryDefinition BuildCosmosQuery(string queryTemplate, JToken? configParameters, ExtractedParameters parameters)
    {
        var queryDefinition = new QueryDefinition(queryTemplate);

        // Standard substitutions
        if (!string.IsNullOrEmpty(parameters.EntityId))
        {
            var rawEntityId = ExtractRawEntityId(parameters.EntityId);
            queryDefinition = queryDefinition.WithParameter("@entityId", rawEntityId);
        }

        if (!string.IsNullOrEmpty(parameters.StartDate))
        {
            queryDefinition = queryDefinition.WithParameter("@startDate", parameters.StartDate);
        }

        if (!string.IsNullOrEmpty(parameters.EndDate))
        {
            queryDefinition = queryDefinition.WithParameter("@endDate", parameters.EndDate);
        }

        // Config-driven parameter substitution
        queryDefinition = SubstituteConfigParameters(queryDefinition, configParameters, parameters);

        return queryDefinition;
    }

    /// <summary>
    /// Extracts the raw entity ID from a CH URI format string.
    /// e.g., 'ch:customer::tpid:123456' → '123456', 'ch:product::id:GUID' → 'GUID'
    /// </summary>
    internal static string ExtractRawEntityId(string entityId)
    {
        if (string.IsNullOrEmpty(entityId))
            return entityId;

        if (!entityId.Contains("ch:", StringComparison.OrdinalIgnoreCase))
            return entityId;

        var lastColon = entityId.LastIndexOf(':');
        return lastColon >= 0 && lastColon < entityId.Length - 1
            ? entityId[(lastColon + 1)..]
            : entityId;
    }

    /// <summary>
    /// Substitutes config-driven parameters (beyond entityId/startDate/endDate) into the query.
    /// Checks FilterValues first, then falls back to config Default values.
    /// </summary>
    private static QueryDefinition SubstituteConfigParameters(QueryDefinition queryDefinition, JToken? configParameters, ExtractedParameters parameters)
    {
        if (configParameters == null)
            return queryDefinition;

        var paramArray = configParameters switch
        {
            JArray array => array,
            JObject obj => new JArray(obj),
            _ => null
        };

        if (paramArray == null || !paramArray.Any())
            return queryDefinition;

        foreach (var paramConfig in paramArray)
        {
            var paramName = paramConfig.Value<string>(CxOAIConstants.Field_Name);
            if (string.IsNullOrEmpty(paramName))
                continue;

            // Skip standard params already handled
            if (string.Equals(paramName, "entityId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "startDate", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paramName, "endDate", StringComparison.OrdinalIgnoreCase))
                continue;

            string? value = null;

            // Check FilterValues first
            if (parameters.FilterValues.TryGetValue(paramName, out var filterValue))
            {
                value = filterValue;
            }

            // Fall back to config default
            if (string.IsNullOrEmpty(value))
            {
                var defaultValue = paramConfig["Default"]?.ToString();
                if (!string.IsNullOrEmpty(defaultValue))
                    value = defaultValue;
            }

            if (value != null)
            {
                queryDefinition = queryDefinition.WithParameter($"@{paramName}", value);
            }
        }

        return queryDefinition;
    }

    /// <summary>
    /// Postprocesses Cosmos DB results: field projection or raw JSON, navigation URL, AgentPrompt injection.
    /// </summary>
    private async Task<string> PostprocessCosmosResponseAsync(
        List<JObject> results, JObject metricConfig,
        ExtractedParameters parameters, string metricName)
    {
        // Read SelectFields from config: AdditionalMetadata.PostProcessingSchemaMapping.SelectFields
        var postProcessingConfig = metricConfig[CxOAIConstants.Field_AdditionalMetadata]?[CxOAIConstants.Field_PostProcessingSchemaMapping];
        var selectFieldsToken = postProcessingConfig?[CxOAIConstants.Field_SelectFields];
        var selectFields = selectFieldsToken?.ToObject<List<string>>();

        string content;
        if (selectFields != null && selectFields.Count > 0)
        {
            _logger.LogInformation("PostprocessCosmosResponse | Projecting fields [{Fields}] for metric {Metric}",
                string.Join(", ", selectFields), metricName);
            content = ProjectFields(results, selectFields);
        }
        else
        {
            content = FormatRawJsonResponse(results, metricName);
        }

        // Append navigation URL
        try
        {
            var navigationUrl = await GetNavigationUrlAsync(metricConfig, parameters);
            if (!string.IsNullOrWhiteSpace(navigationUrl))
            {
                _logger.LogInformation("PostprocessCosmosResponse | Appending navigation URL for metric {Metric}", metricName);
                content += $"\n\n---\nFor more details, see the [CXO Dashboard]({navigationUrl}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostprocessCosmosResponse | Failed to generate navigation URL for metric {Metric}", metricName);
        }

        // Prepend AgentPrompt if configured (supports both "AdditionalMetadata" and "Additionalmetadata" casing)
        var agentPrompt = metricConfig[CxOAIConstants.Field_AdditionalMetadata]?[CxOAIConstants.Field_AgentPrompt]?.ToString();

        if (!string.IsNullOrWhiteSpace(agentPrompt))
        {
            _logger.LogInformation("PostprocessCosmosResponse | Prepending AgentPrompt for metric {Metric}", metricName);
            content = $"<SYSTEM_NOTE>{agentPrompt}</SYSTEM_NOTE>\n\n{content}";
        }

        return content;
    }

    /// <summary>
    /// Projects specific fields from Cosmos results based on config SelectFields.
    /// Single field: returns raw values. Multiple fields: returns projected JSON objects.
    /// </summary>
    internal static string ProjectFields(List<JObject> results, List<string> selectFields)
    {
        if (selectFields.Count == 1)
        {
            var fieldName = selectFields[0];
            var projectedValues = results
                .Select(doc => FindFieldValue(doc, fieldName))
                .Where(v => v != null)
                .ToList();

            if (projectedValues.Count == 1)
                return projectedValues[0]!;

            return string.Join("\n\n---\n\n", projectedValues!);
        }

        // Multiple fields: return projected JSON
        var projectedDocs = results.Select(doc =>
        {
            var projected = new JObject();
            foreach (var field in selectFields)
            {
                var property = doc.Properties()
                    .FirstOrDefault(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));
                if (property != null)
                    projected[property.Name] = property.Value.DeepClone();
            }
            return projected;
        }).ToList();

        if (projectedDocs.Count == 1)
            return projectedDocs[0].ToString(Formatting.Indented);

        return new JArray(projectedDocs).ToString(Formatting.Indented);
    }

    /// <summary>
    /// Formats Cosmos results as raw JSON (single doc: object, multiple: array).
    /// </summary>
    private string FormatRawJsonResponse(List<JObject> results, string metricName)
    {
        if (results.Count == 1)
        {
            _logger.LogInformation("FormatRawJsonResponse | Returning single document for metric {Metric}", metricName);
            return results[0].ToString(Formatting.Indented);
        }

        _logger.LogInformation("FormatRawJsonResponse | Returning {Count} documents for metric {Metric}", results.Count, metricName);
        return new JArray(results).ToString(Formatting.Indented);
    }

    /// <summary>
    /// Finds a field value in a JObject by name (case-insensitive).
    /// </summary>
    private static string? FindFieldValue(JObject doc, string fieldName)
    {
        var property = doc.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        return property?.Value?.ToString();
    }

    #endregion

    #region Kusto Data Source

    /// <summary>
    /// Executes a Kusto (Azure Data Explorer) query based on the metric config and extracted parameters.
    /// Follows the same pattern as ExecuteCosmosAsync: validate config → build query → execute → limit → postprocess.
    /// </summary>
    private async Task<CXOAgentResponse> ExecuteKustoAsync(
        JObject metricConfig, ExtractedParameters parameters, Stopwatch sw)
    {
        var metricName = metricConfig.Value<string>(CxOAIConstants.Field_Name) ?? string.Empty;

        var (baseQuery, defaultTopN, kustoDbConfig, connectionKey) = ValidateKustoConfig(metricConfig, metricName);

        // Build query with parameterized values (prevents KQL injection)
        var configParameters = metricConfig.SelectToken(CxOAIConstants.Field_Parameters);
        var (resolvedQuery, queryParameters) = BuildKustoQuery(baseQuery, configParameters, parameters, metricConfig);

        _logger.LogInformation("ExecuteKustoAsync | Executing query for metric {Metric}, connectionKey={Key}: {Query} with {ParamCount} parameters",
            metricName, connectionKey, resolvedQuery, queryParameters.Count);

        // Execute Kusto query with parameterized values
        await NotifyAsync("🔗 Querying Kusto...");
        var queryStopwatch = Stopwatch.StartNew();
        List<JObject> results;
        try
        {
            results = await ExecuteKustoQueryAsync(kustoDbConfig, resolvedQuery, queryParameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteKustoAsync | Kusto query failed for metric {Metric}", metricName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                Response = $"Error executing Kusto query for metric '{metricName}': {ex.Message}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
        queryStopwatch.Stop();

        // Apply DefaultTopN limit if configured
        if (defaultTopN.HasValue && results.Count > defaultTopN.Value)
        {
            results = results.Take(defaultTopN.Value).ToList();
        }

        _logger.LogInformation("ExecuteKustoAsync | Query returned {Count} results in {ElapsedMs}ms (limited to {Max})",
            results.Count, queryStopwatch.ElapsedMilliseconds, defaultTopN?.ToString() ?? "unlimited");

        if (results.Count == 0)
        {
            sw.Stop();
            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = $"No data found for metric '{metricName}'."
            };
        }

        // Postprocess
        await NotifyAsync("⚙️ Processing response...");
        var content = await PostprocessKustoResponseAsync(results, metricConfig, parameters, metricName);

        sw.Stop();
        _logger.LogInformation("ExecuteKustoAsync | Success: metric={Metric}, results={Count}, duration={ElapsedMs}ms",
            metricName, results.Count, sw.ElapsedMilliseconds);

        await NotifyAsync("✅ Data ready");

        return new CXOAgentResponse
        {
            IsSuccess = true,
            Response = content
        };
    }

    /// <summary>
    /// Validates and resolves Kusto configuration from the metric config and AspectToolsConfig.
    /// Throws InvalidOperationException if any required configuration is missing.
    /// </summary>
    private (string BaseQuery, int? DefaultTopN, AspectKustoDbConfig KustoDbConfig, string ConnectionKey) ValidateKustoConfig(
        JObject metricConfig, string metricName)
    {
        var dataSourceConfig = metricConfig[CxOAIConstants.Field_DataSource]
            ?? throw new InvalidOperationException($"DataSource configuration is missing for metric '{metricName}'.");

        var connectionKey = dataSourceConfig.Value<string>(CxOAIConstants.Field_ConnectionKey) ?? string.Empty;
        var baseQuery = dataSourceConfig.Value<string>(CxOAIConstants.Field_BaseQuery) ?? string.Empty;
        var defaultTopN = dataSourceConfig.Value<int?>(CxOAIConstants.Field_DefaultTopN);

        if (string.IsNullOrWhiteSpace(connectionKey))
            throw new InvalidOperationException($"ConnectionKey is missing in DataSource config for metric '{metricName}'.");

        if (string.IsNullOrWhiteSpace(baseQuery))
            throw new InvalidOperationException($"BaseQuery is missing in DataSource config for metric '{metricName}'.");

        var kustoConnectionMaps = _aspectToolsConfig?.KustoConnectionMaps;
        if (kustoConnectionMaps == null || !kustoConnectionMaps.TryGetValue(connectionKey, out var kustoDbConfig))
            throw new InvalidOperationException($"No Kusto connection configured for key '{connectionKey}' in metric '{metricName}'. Ensure the key exists in kustoConnectionMaps configuration.");

        if (string.IsNullOrWhiteSpace(kustoDbConfig.KustoClusterUrl))
            throw new InvalidOperationException($"KustoClusterUrl is missing for Kusto connection '{connectionKey}'.");

        if (string.IsNullOrWhiteSpace(kustoDbConfig.KustoDatabaseName))
            throw new InvalidOperationException($"KustoDatabaseName is missing for Kusto connection '{connectionKey}'.");

        return (baseQuery, defaultTopN, kustoDbConfig, connectionKey);
    }

    /// <summary>
    /// Executes a KQL query against Azure Data Explorer and returns results as JObject list.
    /// Uses declare query_parameters and ClientRequestProperties.SetParameter for safe parameterized execution.
    /// </summary>
    private async Task<List<JObject>> ExecuteKustoQueryAsync(
        AspectKustoDbConfig config, string query, Dictionary<string, string> queryParameters)
    {
        var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(config.KustoClusterUrl)
        {
            InitialCatalog = config.KustoDatabaseName
        };

#if DEBUG
        kustoConnectionStringBuilder = kustoConnectionStringBuilder
            .WithAadAzureTokenCredentialsAuthentication(new Azure.Identity.DefaultAzureCredential());
#else
        if (!string.IsNullOrEmpty(config.CredentialConfig?.MuiClientId))
        {
            kustoConnectionStringBuilder = kustoConnectionStringBuilder
                .WithAadAzureTokenCredentialsAuthentication(
                    new Azure.Identity.ManagedIdentityCredential(config.CredentialConfig.MuiClientId));
        }
        else
        {
            kustoConnectionStringBuilder = kustoConnectionStringBuilder
                .WithAadAzureTokenCredentialsAuthentication(new Azure.Identity.ManagedIdentityCredential());
        }
#endif

        var timeout = TimeSpan.FromMinutes(5);
        using var queryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionStringBuilder);
        var clientRequestProperties = new ClientRequestProperties();
        clientRequestProperties.SetOption(ClientRequestProperties.OptionServerTimeout, timeout);

        // Bind parameters safely via declare query_parameters and SetParameter
        if (queryParameters.Count > 0)
        {
            var paramDeclarations = string.Join(", ", queryParameters.Keys.Select(k => $"{k}:string"));
            query = $"declare query_parameters({paramDeclarations});\n{query}";

            _logger.LogInformation("ExecuteKustoQueryAsync | query : {query}", query);

            foreach (var param in queryParameters)
            {
                clientRequestProperties.SetParameter(param.Key, param.Value);
            }
        }

        using var reader = await queryProvider.ExecuteQueryAsync(
            config.KustoDatabaseName, query, clientRequestProperties);

        return SerializeReaderToJObjects(reader);
    }

    /// <summary>
    /// Converts an IDataReader to a list of JObjects.
    /// </summary>
    private static List<JObject> SerializeReaderToJObjects(IDataReader reader)
    {
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var results = new List<JObject>();
        while (reader.Read())
        {
            var row = new JObject();
            foreach (var col in columns)
            {
                var value = reader[col];
                row[col] = value == DBNull.Value ? null : JToken.FromObject(value);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Builds a parameterized Kusto query by collecting parameter references and their values.
    /// Uses KQL declare query_parameters pattern to prevent injection.
    /// </summary>
    internal static (string Query, Dictionary<string, string> QueryParameters) BuildKustoQuery(
        string baseQuery, JToken? configParameters, ExtractedParameters parameters, JObject metricConfig)
    {
        var queryParameters = new Dictionary<string, string>();
        var resolvedQuery = SubstituteKustoParameters(baseQuery, configParameters as JArray, parameters, queryParameters);
        resolvedQuery = AppendKustoFilterExpressions(resolvedQuery, metricConfig, parameters, queryParameters);
        resolvedQuery = AppendKustoGlobalFilterExpressions(resolvedQuery, parameters.GlobalLevelFilters, queryParameters);
        return (resolvedQuery, queryParameters);
    }

    /// <summary>
    /// Replaces {paramName} placeholders in the base query with KQL parameter references (__p_paramName)
    /// and collects the actual values into the queryParameters dictionary for safe parameterized execution.
    /// </summary>
    internal static string SubstituteKustoParameters(
        string baseQuery, JArray? configParameters, ExtractedParameters parameters,
        Dictionary<string, string> queryParameters)
    {
        var resolvedQuery = baseQuery;

        if (configParameters == null || !configParameters.Any())
            return resolvedQuery;

        foreach (var paramConfig in configParameters)
        {
            var paramName = paramConfig.Value<string>("Name");
            if (string.IsNullOrEmpty(paramName))
                continue;

            string? value = null;

            if (parameters.FilterValues.TryGetValue(paramName, out var filterValue))
            {
                value = filterValue;
            }
            else if (string.Equals(paramName, "customerId", StringComparison.OrdinalIgnoreCase))
            {
                value = ExtractRawEntityIdBySegment(parameters.EntityId, "tpid");
            }
            else if (string.Equals(paramName, "entityId", StringComparison.OrdinalIgnoreCase))
            {
                value = parameters.EntityId;
            }
            else if (string.Equals(paramName, "startDate", StringComparison.OrdinalIgnoreCase))
            {
                value = parameters.StartDate;
            }
            else if (string.Equals(paramName, "endDate", StringComparison.OrdinalIgnoreCase))
            {
                value = parameters.EndDate;
            }

            // Fall back to config default
            if (string.IsNullOrEmpty(value))
            {
                var defaultValue = paramConfig["Default"]?.ToString();
                value = defaultValue;
            }

            if (value != null)
            {
                var kqlParamName = $"_p_{SanitizeKqlParamName(paramName)}";
                // Replace quoted placeholder '{paramName}' → bare parameter reference
                resolvedQuery = resolvedQuery.Replace($"'{{{paramName}}}'", kqlParamName);
                // Replace unquoted placeholder {paramName} → bare parameter reference
                resolvedQuery = resolvedQuery.Replace($"{{{paramName}}}", kqlParamName);
                queryParameters[kqlParamName] = value;
            }
        }

        return resolvedQuery;
    }

    /// <summary>
    /// Extracts a raw identifier segment from a CH URI format entity ID.
    /// For "ch:customer::tpid:784852" with segment "tpid", returns "784852".
    /// </summary>
    internal static string? ExtractRawEntityIdBySegment(string? entityId, string segment)
    {
        if (string.IsNullOrEmpty(entityId))
            return entityId;

        if (!entityId.StartsWith("ch:", StringComparison.OrdinalIgnoreCase))
            return entityId;

        var segmentPrefix = $"{segment}:";
        var idx = entityId.IndexOf(segmentPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return entityId[(idx + segmentPrefix.Length)..];

        // Fallback: return last segment
        var lastColon = entityId.LastIndexOf(':');
        return lastColon >= 0 && lastColon < entityId.Length - 1
            ? entityId[(lastColon + 1)..]
            : entityId;
    }

    /// <summary>
    /// Appends parameterized Kusto WHERE clauses from config Filters[].KustoExpression.
    /// Uses __f_ prefixed parameter references instead of embedding values directly.
    /// </summary>
    internal static string AppendKustoFilterExpressions(
        string resolvedQuery, JObject metricConfig, ExtractedParameters parameters,
        Dictionary<string, string> queryParameters)
    {
        var filtersArray = metricConfig.SelectToken(CxOAIConstants.Field_Filters) as JArray;
        if (filtersArray == null || !filtersArray.Any())
            return resolvedQuery;

        var filterClauses = new List<string>();

        foreach (var filter in filtersArray)
        {
            var filterName = filter.Value<string>("Name");
            if (string.IsNullOrEmpty(filterName))
                continue;

            var kustoExpression = filter.Value<string>(CxOAIConstants.Field_KustoExpression);
            if (string.IsNullOrEmpty(kustoExpression))
                continue;

            if (!parameters.FilterValues.TryGetValue(filterName, out var filterValue))
                continue;
            if (string.IsNullOrWhiteSpace(filterValue))
                continue;

            var filterType = filter.Value<string>("Type")?.ToLowerInvariant();
            var sanitizedName = SanitizeKqlParamName(filterName);

            string resolvedExpression;
            if (string.Equals(filterType, "array", StringComparison.OrdinalIgnoreCase))
            {
                var arrayValues = filterValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var paramNames = new List<string>();
                for (int i = 0; i < arrayValues.Length; i++)
                {
                    var arrayParamName = $"_f_{sanitizedName}_{i}";
                    paramNames.Add(arrayParamName);
                    queryParameters[arrayParamName] = arrayValues[i].Trim('\'');
                }
                resolvedExpression = kustoExpression.Replace("{value}", string.Join(", ", paramNames));
            }
            else
            {
                var filterParamName = $"_f_{sanitizedName}";
                resolvedExpression = kustoExpression.Replace("{value}", filterParamName);
                queryParameters[filterParamName] = filterValue.Trim('\'');
            }

            filterClauses.Add(resolvedExpression);
        }

        if (filterClauses.Count == 0)
            return resolvedQuery;

        var combinedFilter = string.Join(" and ", filterClauses);
        return $"{resolvedQuery}\n| where {combinedFilter}";
    }

    /// <summary>
    /// Appends parameterized Kusto WHERE clauses from GlobalLevelFilters.
    /// Uses _g_ prefixed parameter references with type-aware wrapping.
    /// </summary>
    internal static string AppendKustoGlobalFilterExpressions(
        string resolvedQuery, List<ViewFilter>? globalFilters, Dictionary<string, string> queryParameters)
    {
        if (globalFilters == null || !globalFilters.Any())
            return resolvedQuery;

        var filterClauses = new List<string>();
        int paramIndex = 0;

        foreach (var filter in globalFilters)
        {
            if (string.IsNullOrWhiteSpace(filter.Column))
                continue;

            var filterClause = filter.FilterClause?.ToLowerInvariant() ?? "==";

            if (filterClause == "contains" && filter.ContainsSelectedValues?.Any() == true)
            {
                var containsExpressions = new List<string>();
                foreach (var v in filter.ContainsSelectedValues.Where(v => !string.IsNullOrWhiteSpace(v)))
                {
                    var paramName = $"_g_{paramIndex++}";
                    queryParameters[paramName] = v;
                    containsExpressions.Add($"{filter.Column} contains {paramName}");
                }

                if (containsExpressions.Count == 1)
                    filterClauses.Add(containsExpressions[0]);
                else if (containsExpressions.Count > 1)
                    filterClauses.Add($"({string.Join(" or ", containsExpressions)})");
            }
            else if (filterClause == "!=" && filter.SelectedValues?.Any() == true)
            {
                var values = filter.SelectedValues.Select(v => v?.ToString() ?? string.Empty).Where(v => !string.IsNullOrEmpty(v)).ToList();
                if (values.Count == 1)
                {
                    var paramName = $"_g_{paramIndex++}";
                    queryParameters[paramName] = values[0];
                    filterClauses.Add($"{filter.Column} != {WrapKustoParamWithType(paramName, filter.SelectedValues![0]!)}");
                }
                else if (values.Count > 1)
                {
                    var paramNames = new List<string>();
                    for (int i = 0; i < values.Count; i++)
                    {
                        var paramName = $"_g_{paramIndex++}";
                        queryParameters[paramName] = values[i];
                        paramNames.Add(WrapKustoParamWithType(paramName, filter.SelectedValues![i]!));
                    }
                    filterClauses.Add($"{filter.Column} !in ({string.Join(", ", paramNames)})");
                }
            }
            else if (filterClause == "==" && filter.SelectedValues?.Any() == true)
            {
                var values = filter.SelectedValues.Select(v => v?.ToString() ?? string.Empty).Where(v => !string.IsNullOrEmpty(v)).ToList();
                if (values.Count == 1)
                {
                    var paramName = $"_g_{paramIndex++}";
                    queryParameters[paramName] = values[0];
                    filterClauses.Add($"{filter.Column} == {WrapKustoParamWithType(paramName, filter.SelectedValues![0]!)}");
                }
                else if (values.Count > 1)
                {
                    var paramNames = new List<string>();
                    for (int i = 0; i < values.Count; i++)
                    {
                        var paramName = $"_g_{paramIndex++}";
                        queryParameters[paramName] = values[i];
                        paramNames.Add(WrapKustoParamWithType(paramName, filter.SelectedValues![i]!));
                    }
                    filterClauses.Add($"{filter.Column} in ({string.Join(", ", paramNames)})");
                }
            }
        }

        if (filterClauses.Count == 0)
            return resolvedQuery;

        var combinedFilter = string.Join(" and ", filterClauses);
        return $"{resolvedQuery}\n| where {combinedFilter}";
    }

    /// <summary>
    /// Formats a value for Kusto filter expression based on its type.
    /// </summary>
    internal static string FormatKustoValue(object value)
    {
        return value switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            int i => i.ToString(),
            long l => l.ToString(),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal dec => dec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => $"'{EscapeKustoValue(value.ToString() ?? string.Empty)}'"
        };
    }

    /// <summary>
    /// Escapes single quotes in Kusto string values by replacing ' with ''.
    /// </summary>
    internal static string EscapeKustoValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    /// <summary>
    /// Sanitizes a name for use as a KQL parameter identifier.
    /// Replaces non-alphanumeric characters (except underscore) with underscore.
    /// </summary>
    internal static string SanitizeKqlParamName(string name)
    {
        return new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }

    /// <summary>
    /// Wraps a KQL parameter name with the appropriate type conversion function
    /// based on the original value's CLR type (e.g., tolong, todouble, tobool).
    /// String values are returned as-is since KQL parameters default to string.
    /// </summary>
    internal static string WrapKustoParamWithType(string paramName, object value)
    {
        return value switch
        {
            bool => $"tobool({paramName})",
            int or long or short or byte => $"tolong({paramName})",
            double or float or decimal => $"todouble({paramName})",
            _ => paramName
        };
    }

    /// <summary>
    /// Postprocesses Kusto results: List formatting (Fields, FieldRenames, MessageTemplate), raw JSON fallback,
    /// navigation URL, and AgentPrompt injection.
    /// </summary>
    private async Task<string> PostprocessKustoResponseAsync(
        List<JObject> results, JObject metricConfig,
        ExtractedParameters parameters, string metricName)
    {
        var additionalMetadata = metricConfig[CxOAIConstants.Field_AdditionalMetadata];
        var listConfig = additionalMetadata?[CxOAIConstants.Field_PostProcessingSchemaMapping]?["List"] as JObject;

        string content;
        if (listConfig != null)
        {
            _logger.LogInformation("PostprocessKustoResponse | Formatting with List config for metric {Metric}", metricName);
            content = FormatKustoListResults(results, listConfig);
        }
        else
        {
            content = FormatRawJsonResponse(results, metricName);
        }

        // Append navigation URL
        try
        {
            var navigationUrl = await GetNavigationUrlAsync(metricConfig, parameters);
            if (!string.IsNullOrWhiteSpace(navigationUrl))
            {
                _logger.LogInformation("PostprocessKustoResponse | Appending navigation URL for metric {Metric}", metricName);
                content += $"\n\n---\nFor more details, see the [CXO Dashboard]({navigationUrl}).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostprocessKustoResponse | Failed to generate navigation URL for metric {Metric}", metricName);
        }

        // Prepend AgentPrompt if configured
        var agentPrompt = additionalMetadata?[CxOAIConstants.Field_AgentPrompt]?.ToString();
        if (!string.IsNullOrWhiteSpace(agentPrompt))
        {
            _logger.LogInformation("PostprocessKustoResponse | Prepending AgentPrompt for metric {Metric}", metricName);
            content = $"<SYSTEM_NOTE>{agentPrompt}</SYSTEM_NOTE>\n\n{content}";
        }

        return content;
    }

    /// <summary>
    /// Formats Kusto results using List config: projects Fields, applies FieldRenames, wraps in MessageTemplate.
    /// </summary>
    private static string FormatKustoListResults(List<JObject> results, JObject listConfig)
    {
        var fields = listConfig["Fields"]?.ToObject<List<string>>();
        var fieldRenames = listConfig["FieldRenames"] as JObject;
        var messageTemplate = listConfig.Value<string>("MessageTemplate") ?? "{data}";

        var formattedRows = new List<string>();
        foreach (var row in results)
        {
            var outputParts = new List<string>();

            if (fields != null && fields.Count > 0)
            {
                foreach (var field in fields)
                {
                    var displayName = fieldRenames?.Value<string>(field) ?? field;
                    var value = row.Value<string>(field) ?? string.Empty;
                    outputParts.Add($"**{displayName}**: {value}");
                }
            }
            else
            {
                foreach (var prop in row.Properties())
                {
                    var displayName = fieldRenames?.Value<string>(prop.Name) ?? prop.Name;
                    outputParts.Add($"**{displayName}**: {prop.Value}");
                }
            }

            formattedRows.Add(string.Join(" | ", outputParts));
        }

        var formattedData = string.Join("\n", formattedRows);
        return messageTemplate.Replace("{data}", formattedData);
    }

    #endregion

    #region String Utilities (moved from StringUtils)

    /// <summary>
    /// Builds a RadioButtonGroup UI component JSON string from search results.
    /// Each option displays "Name(RawId)" where RawId is extracted from the CH URI.
    /// </summary>
    /// <param name="results">The search result JObjects.</param>
    /// <param name="title">The component title (e.g., "Customers", "Products").</param>
    /// <param name="label">The prompt label shown to the user.</param>
    /// <param name="nameField">The JObject field name containing the display name (e.g., "Customer Name", "Product Name").</param>
    /// <param name="idField">The JObject field name containing the CH URI id (e.g., "Customer Id", "Product Id").</param>
    internal static string BuildRadioButtonGroup(List<JObject> results, string title, string label, string nameField, string idField)
    {
        var options = results.Select(r =>
        {
            var name = r[nameField]?.ToString() ?? string.Empty;
            var id = r[idField]?.ToString() ?? string.Empty;
            var rawId = ExtractRawEntityId(id);
            var displayText = $"{name}({rawId})";
            var keyText = $"{name}({id})";
            return new JObject
            {
                ["key"] = keyText,
                ["text"] = displayText
            };
        }).ToList();

        //var defaultKey = options.FirstOrDefault()?["key"]?.ToString() ?? string.Empty;

        var component = new JObject
        {
            ["componentType"] = "RadioButtonGroup",
            ["title"] = title,
            ["props"] = new JObject
            {
                ["label"] = label,
                ["options"] = new JArray(options),
                //["defaultSelectedKey"] = defaultKey
            }
        };

        return component.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// Used for fuzzy matching of workload types and program names.
    /// </summary>
    public static int GetLevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
            return string.IsNullOrEmpty(b) ? 0 : b.Length;

        if (string.IsNullOrEmpty(b))
            return a.Length;

        if (a.Length < b.Length)
            (a, b) = (b, a);

        int[] previousRow = new int[b.Length + 1];
        int[] currentRow = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
            previousRow[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            currentRow[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[b.Length];
    }

    /// <summary>
    /// Formats a customer name for Insights API search by escaping special characters
    /// and joining words with "AND". Matches the CXO search bar formatting.
    /// </summary>
    /// <example>"Contoso Ltd." → "contoso AND ltd\."</example>
    public static string FormatCustomerSearchField(string customerName)
    {
        return string.Join(" AND ",
            customerName.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => string.Concat(
                    word.Select(c => char.IsLetterOrDigit(c)
                        ? c.ToString()
                        : $"\\{c}")
                )));
    }

    /// <summary>
    /// Formats a product name for Insights API search by escaping special characters,
    /// joining words with "AND", and adding wildcard prefix/suffix.
    /// </summary>
    /// <example>"Azure SQL" → "\*azure AND sql*"</example>
    public static string FormatProductSearchField(string productName)
    {
        productName = productName.Trim();

        var sb = new System.Text.StringBuilder();
        bool previousWasSpecialOrSpace = false;

        int lastAlphanumericPos = -1;
        for (int i = productName.Length - 1; i >= 0; i--)
        {
            if (char.IsLetterOrDigit(productName[i]))
            {
                lastAlphanumericPos = i;
                break;
            }
        }

        for (int i = 0; i < productName.Length; i++)
        {
            char c = productName[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                previousWasSpecialOrSpace = false;
            }
            else
            {
                if (!previousWasSpecialOrSpace && i < lastAlphanumericPos)
                {
                    sb.Append(" AND ");
                    previousWasSpecialOrSpace = true;
                }
            }
        }

        return "\\*" + sb.ToString().ToLower() + "*";
    }

    private static object? UnwrapJsonValue(object? value)
    {
        if (value == null)
            return null;

        if (value is JValue jValue)
            return jValue.Value;

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                System.Text.Json.JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal) ? longVal : jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Null => null,
                System.Text.Json.JsonValueKind.Undefined => null,
                _ => jsonElement.ToString()
            };
        }

        return value;
    }

    private static List<object> UnwrapJsonValues(IEnumerable<object>? values)
    {
        if (values == null)
            return new List<object>();

        return values
            .Select(UnwrapJsonValue)
            .Where(v => v != null)
            .Cast<object>()
            .ToList();
    }

    #endregion

    #region Navigation URL

    private static readonly HttpClient _navHttpClient = new();

    /// <summary>
    /// Generates a CxObserve navigation URL for the given metric and parameters.
    /// Tries temp filter URL first (if filters applied), falls back to simple URL.
    ///
    /// Changes from old repo:
    /// - Moved inline into AspectTools (was a separate NavigationUrlProvider service).
    /// - Uses CxOAIConstants for JSON field names instead of raw strings.
    /// - Gets userToken from _authContext instead of ToolExecutionContext.Current.
    /// - Does not depend on IMetadataService; uses a self-contained HTTP POST helper.
    /// </summary>
    internal async Task<string> GetNavigationUrlAsync(JObject metricConfig, ExtractedParameters extractedParameters)
    {
        var metricName = metricConfig[CxOAIConstants.Field_Name]?.ToString();
        string navigationLink = string.Empty;

        try
        {
            // Skip navigation URL for entity-less metrics (empty SupportedEntityTypes)
            var supportedEntities = metricConfig[CxOAIConstants.Field_SupportedEntityTypes]?.ToObject<List<string>>() ?? [];
            if (supportedEntities.Count == 0)
            {
                _logger.LogInformation("Skipping navigation URL for entity-less metric {MetricName}", metricName);
                return string.Empty;
            }

            await NotifyAsync("🔗 Generating dashboard link...");
            var userToken = _authContext.AccessToken ?? string.Empty;
            var tempFilterResult = await GenerateTempFilterUrlAsync(metricConfig, extractedParameters, userToken);

            _logger.LogInformation("TempFilterResult - HasFilters: {HasFilters}, Url: {Url}, Error: {Error}",
                tempFilterResult.HasFilters, tempFilterResult.Url, tempFilterResult.Error);

            if (tempFilterResult.HasFilters && !string.IsNullOrEmpty(tempFilterResult.Url))
            {
                navigationLink = tempFilterResult.Url;
            }
            else if (!tempFilterResult.HasFilters)
            {
                navigationLink = GenerateSimpleNavigationUrl(metricConfig, extractedParameters);
            }
            else if (!string.IsNullOrEmpty(tempFilterResult.Error))
            {
                _logger.LogWarning("Failed to generate temp filter URL: {Error}, falling back to simple URL", tempFilterResult.Error);
                navigationLink = GenerateSimpleNavigationUrl(metricConfig, extractedParameters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception generating navigation link, continuing without link");
        }

        _logger.LogInformation("Navigation URL for {MetricName}: {Url}", metricName, navigationLink);
        return navigationLink;
    }

    /// <summary>
    /// Builds a simple navigation URL without temp filters.
    /// Format: {baseUrl}/{pageType}/{entityId}/{viewPath}?startDate=&amp;endDate=&amp;unit=&amp;highLightId=
    /// </summary>
    internal string GenerateSimpleNavigationUrl(JObject metricConfig, ExtractedParameters extractedParams)
    {
        var additionalMetadata = metricConfig[CxOAIConstants.Field_AdditionalMetadata];
        var metricUiComponentMap = additionalMetadata?[CxOAIConstants.Field_MetricUIComponentMap]?.ToString();
        var metricViewPath = additionalMetadata?[CxOAIConstants.Field_MetricViewPath]?.ToString();

        var pageType = DomainTempFilterConfig.GetPageType(extractedParams.EntityId);
        var viewPath = metricViewPath ?? "summary";
        var url = $"{_aspectToolsConfig?.CxObserveBaseUrl}/{pageType}/{extractedParams.EntityId}/{viewPath}";

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(extractedParams.StartDate))
            queryParams.Add($"startDate={Uri.EscapeDataString(extractedParams.StartDate)}");
        if (!string.IsNullOrEmpty(extractedParams.EndDate))
            queryParams.Add($"endDate={Uri.EscapeDataString(extractedParams.EndDate)}");
        if (!string.IsNullOrEmpty(extractedParams.Unit))
            queryParams.Add($"unit={extractedParams.Unit}");
        if (!string.IsNullOrEmpty(metricUiComponentMap))
            queryParams.Add($"highLightId={metricUiComponentMap}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return url;
    }

    /// <summary>
    /// Generates a temp filter URL if filters were applied.
    /// Posts filter payloads to metadata service, then builds URL with GUID reference.
    ///
    /// Changes from old repo:
    /// - Uses self-contained HttpPostJsonAsync instead of IMetadataService.
    /// - Uses CxOAIConstants for JSON field access.
    /// </summary>
    internal async Task<TempFilterUrlResult> GenerateTempFilterUrlAsync(
        JObject metricConfig,
        ExtractedParameters extractedParams,
        string userToken)
    {
        var result = new TempFilterUrlResult();
        var metricName = metricConfig[CxOAIConstants.Field_Name]?.ToString();

        try
        {
            var additionalMetadata = metricConfig[CxOAIConstants.Field_AdditionalMetadata];
            var tempFilterViewEnabled = additionalMetadata?[CxOAIConstants.Field_TempFilterViewEnabled]?.ToString();
            if (!string.IsNullOrWhiteSpace(tempFilterViewEnabled) &&
                tempFilterViewEnabled.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("TempFilterView disabled for metric {MetricName}", metricName);
                return result;
            }

            var aspectUrl = metricConfig[CxOAIConstants.Field_DataSource]?[CxOAIConstants.Field_SystemUrl]?.ToString();
            var viewPath = additionalMetadata?[CxOAIConstants.Field_MetricViewPath]?.ToString();
            var domain = metricConfig[CxOAIConstants.Field_Domain]?.ToString() ?? string.Empty;

            var pageType = DomainTempFilterConfig.GetPageType(extractedParams.EntityId);
            var domainSettings = DomainTempFilterConfig.GetSettings(domain, pageType, aspectUrl, viewPath);
            if (domainSettings == null)
            {
                _logger.LogWarning("No domain settings found for domain '{Domain}', viewPath '{ViewPath}', aspect '{AspectUrl}'",
                    domain, viewPath, aspectUrl);
                return result;
            }

            var (pageLevelViewFilters, globalLevelViewFilters) = BuildViewFilters(metricConfig, extractedParams);

            if (pageLevelViewFilters.Count == 0 && globalLevelViewFilters.Count == 0)
            {
                _logger.LogInformation("No filters applied for metric {MetricName}, skipping temp view generation", metricName);
                return result;
            }

            var guid = Guid.NewGuid().ToString();
            var appliedFilters = new List<string>();
            var postTasks = new List<(Task<bool> Task, string FilterType)>();

            if (pageLevelViewFilters.Count > 0)
            {
                var pageLevelFilterRoot = new FilterRoot { Filters = pageLevelViewFilters };
                postTasks.Add((PostFiltersToMetadataAsync(pageLevelFilterRoot, guid, userToken, domainSettings.FilterKeyPrefix), "page-level"));
                appliedFilters.AddRange(pageLevelViewFilters.Select(f => f.Name));
            }

            if (globalLevelViewFilters.Count > 0)
            {
                var globalLevelFilterRoot = new FilterRoot { Filters = globalLevelViewFilters };
                postTasks.Add((PostFiltersToMetadataAsync(globalLevelFilterRoot, guid, userToken, domainSettings.GroupKey), "global-level"));
                appliedFilters.AddRange(globalLevelViewFilters.Select(f => f.Name));
            }

            var remainingTasks = new List<(Task<bool> Task, string FilterType)>(postTasks);
            var anySuccess = false;

            while (remainingTasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(remainingTasks.Select(t => t.Task));
                var completedItem = remainingTasks.First(t => t.Task == completedTask);
                remainingTasks.Remove(completedItem);

                try
                {
                    var success = await completedTask;
                    if (success)
                    {
                        anySuccess = true;
                        _logger.LogInformation("Filter POST for '{FilterType}' succeeded", completedItem.FilterType);
                        break;
                    }
                    else
                    {
                        _logger.LogWarning("Filter POST for '{FilterType}' returned false", completedItem.FilterType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Filter POST for '{FilterType}' failed", completedItem.FilterType);
                }
            }

            if (!anySuccess)
            {
                _logger.LogError("All filter POST operations failed for metric {MetricName}", metricName);
                result.Error = "Failed to create temp filter view";
                return result;
            }

            result.Url = BuildTempFilterUrl(metricConfig, extractedParams, guid);
            result.HasFilters = true;
            result.AppliedFilters = appliedFilters;

            _logger.LogInformation("Generated temp filter URL for {MetricName} with {Count} filters: {Url}",
                metricName, appliedFilters.Count, result.Url);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating temp filter URL for metric {MetricName}", metricName);
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Generates navigation URL for entity-less page views (allcustomers, productsearch, allprograms).
    /// Format: {baseUrl}/{viewPath}/views/tempFilter?tempQueryId={guid}
    /// </summary>
    internal async Task<string> GetPageViewNavigationUrlAsync(JObject metricConfig, List<ViewFilter> appliedViewFilters)
    {
        var metricName = metricConfig[CxOAIConstants.Field_Name]?.ToString();

        try
        {
            var additionalMetadata = metricConfig[CxOAIConstants.Field_AdditionalMetadata];
            var viewPath = additionalMetadata?[CxOAIConstants.Field_MetricViewPath]?.ToString();
            var domain = metricConfig[CxOAIConstants.Field_Domain]?.ToString() ?? string.Empty;

            var domainSettings = DomainTempFilterConfig.GetSettings(domain, string.Empty, viewPath: viewPath);
            if (domainSettings == null)
            {
                _logger.LogWarning("No domain settings for page view metric '{MetricName}', viewPath '{ViewPath}'", metricName, viewPath);
                return string.Empty;
            }

            var filtersWithValues = appliedViewFilters
                .Where(f => (f.SelectedValues?.Count > 0) ||
                            (f.ContainsSelectedValues?.Count > 0) ||
                            f.From != null ||
                            f.To != null)
                .ToList();

            if (filtersWithValues.Count == 0)
            {
                _logger.LogInformation("No filter values for page view metric '{MetricName}', returning simple URL", metricName);
                return $"{_aspectToolsConfig?.CxObserveBaseUrl}/{viewPath}";
            }

            var guid = Guid.NewGuid().ToString();
            var filterRoot = new FilterRoot { Filters = filtersWithValues };
            var userToken = _authContext.AccessToken ?? string.Empty;

            var postSuccess = await PostFiltersToMetadataAsync(filterRoot, guid, userToken, domainSettings.FilterKeyPrefix);
            if (!postSuccess)
            {
                _logger.LogWarning("Failed to POST page view filters for metric '{MetricName}', returning simple URL", metricName);
                return $"{_aspectToolsConfig?.CxObserveBaseUrl}/{viewPath}";
            }

            var url = $"{_aspectToolsConfig?.CxObserveBaseUrl}/{viewPath}/views/tempFilter?tempQueryId={guid}";
            _logger.LogInformation("Generated page view temp filter URL for '{MetricName}': {Url}", metricName, url);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating page view navigation URL for metric '{MetricName}'", metricName);
            return string.Empty;
        }
    }

    /// <summary>
    /// Builds the complete temp filter URL with query parameters.
    /// Format: {baseUrl}/{pageType}/{entityId}/views/tempfilters/{guid}/{viewPath}?startDate=...&amp;endDate=...&amp;isRelative=false&amp;highLightId=...
    /// </summary>
    private string BuildTempFilterUrl(JObject metricConfig, ExtractedParameters extractedParams, string guid)
    {
        var additionalMetadata = metricConfig[CxOAIConstants.Field_AdditionalMetadata];
        var viewPath = additionalMetadata?[CxOAIConstants.Field_MetricViewPath]?.ToString() ?? "summary";
        var metricUIComponentMap = additionalMetadata?[CxOAIConstants.Field_MetricUIComponentMap]?.ToString();

        var pageType = DomainTempFilterConfig.GetPageType(extractedParams.EntityId);
        var url = $"{_aspectToolsConfig?.CxObserveBaseUrl}/{pageType}/{extractedParams.EntityId}/views/tempfilters/{guid}/{viewPath}";

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(extractedParams.StartDate))
            queryParams.Add($"startDate={Uri.EscapeDataString(extractedParams.StartDate)}");
        if (!string.IsNullOrEmpty(extractedParams.EndDate))
            queryParams.Add($"endDate={Uri.EscapeDataString(extractedParams.EndDate)}");
        if (!string.IsNullOrEmpty(extractedParams.Unit))
            queryParams.Add($"unit={extractedParams.Unit}");
        queryParams.Add("isRelative=false");
        if (!string.IsNullOrEmpty(metricUIComponentMap))
            queryParams.Add($"highLightId={metricUIComponentMap}");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return url;
    }

    /// <summary>
    /// Builds ViewFilter lists from extracted parameters and metric config.
    /// Separates into page-level and global-level filters based on FilterConfig.IsGlobal.
    /// Also processes GlobalLevelFilters from the request context.
    /// </summary>
    private (List<ViewFilter> pageLevelViewFilters, List<ViewFilter> globalLevelViewFilters) BuildViewFilters(
        JObject metricConfig, ExtractedParameters extractedParams)
    {
        var pageLevelViewFilters = new List<ViewFilter>();
        var globalLevelViewFilters = new List<ViewFilter>();

        var filtersArray = metricConfig[CxOAIConstants.Field_Filters] as Newtonsoft.Json.Linq.JArray;
        var hasMetricFilters = extractedParams.FilterValues.Count > 0;
        var hasGlobalFilters = extractedParams.GlobalLevelFilters?.Count > 0;

        if (filtersArray == null || filtersArray.Count == 0 || (!hasMetricFilters && !hasGlobalFilters))
        {
            _logger.LogInformation("No filters in config or no extracted filter values");
            return (pageLevelViewFilters, globalLevelViewFilters);
        }

        var filterConfigs = filtersArray.ToObject<List<FilterConfig>>() ?? new List<FilterConfig>();
        var addedFilterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filterConfig in filterConfigs)
        {
            if (!filterConfig.IsActive || string.IsNullOrEmpty(filterConfig.Name))
                continue;
            if (!extractedParams.FilterValues.TryGetValue(filterConfig.Name, out var value))
                continue;

            var columnType = DomainTempFilterConfig.GetUiColumnType(filterConfig.Type);
            var columnName = filterConfig.BackendFilterName ?? filterConfig.Name;

            var viewFilter = new ViewFilter
            {
                Name = filterConfig.Name,
                Column = columnName,
                ColumnType = columnType,
                ServerColumnType = columnType,
                SelectedValues = NormalizeFilterValue(value, filterConfig.Type),
                FilterClause = "==",
                DefaultSelectedValues = new List<object>(),
                ContainsSelectedValues = new List<string>()
            };

            if (filterConfig.IsGlobal)
                globalLevelViewFilters.Add(viewFilter);
            else
                pageLevelViewFilters.Add(viewFilter);

            addedFilterNames.Add(filterConfig.Name);
        }

        // Process global filters from request context
        if (extractedParams.GlobalLevelFilters?.Count > 0)
        {
            foreach (var globalFilter in extractedParams.GlobalLevelFilters)
            {
                if (addedFilterNames.Contains(globalFilter.Name))
                    continue;

                var matchingFilterConfig = filterConfigs.FirstOrDefault(fc =>
                    fc.IsActive && fc.Name.Equals(globalFilter.Name, StringComparison.OrdinalIgnoreCase));
                if (matchingFilterConfig == null)
                    continue;

                var columnName = matchingFilterConfig.BackendFilterName ?? matchingFilterConfig.Name;
                var selectedValues = UnwrapJsonValues(globalFilter.SelectedValues);
                var containsSelectedValues = globalFilter.ContainsSelectedValues ?? new List<string>();

                if (selectedValues.Count == 0 && containsSelectedValues.Count == 0)
                    continue;

                globalLevelViewFilters.Add(new ViewFilter
                {
                    Name = globalFilter.Name,
                    Column = columnName,
                    ColumnType = globalFilter.ColumnType,
                    ServerColumnType = globalFilter.ColumnType,
                    SelectedValues = selectedValues,
                    FilterClause = globalFilter.FilterClause ?? "==",
                    DefaultSelectedValues = new List<object>(),
                    ContainsSelectedValues = containsSelectedValues
                });

                addedFilterNames.Add(globalFilter.Name);
            }
        }

        return (pageLevelViewFilters, globalLevelViewFilters);
    }

    /// <summary>
    /// Posts filter details to the metadata service for temp filter persistence.
    /// Endpoint: {metadataBaseApiUrl}/api/filterdetails/{filterKeyPrefix}_{guid}/true
    ///
    /// Change from old repo: Self-contained HTTP POST; no dependency on IMetadataService or IHttpClientProvider.
    /// </summary>
    private async Task<bool> PostFiltersToMetadataAsync(FilterRoot filterRoot, string guid, string userToken, string filterKeyPrefix)
    {
        var url = $"{_aspectToolsConfig?.MetadataBaseApiUrl}/api/filterdetails/{filterKeyPrefix}_{guid}/true";

        try
        {
            _logger.LogInformation("PostFiltersToMetadataAsync - URL: {Url}, Prefix: {Prefix}, Filters: {Count}",
                url, filterKeyPrefix, filterRoot.Filters.Count);

            var success = await HttpPostJsonAsync(url, filterRoot, userToken);
            if (!success)
                _logger.LogWarning("PostFiltersToMetadataAsync returned false for prefix '{Prefix}'", filterKeyPrefix);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostFiltersToMetadataAsync failed for prefix '{Prefix}'", filterKeyPrefix);
            return false;
        }
    }

    /// <summary>
    /// Generic HTTP POST helper with Bearer token auth and JSON payload.
    /// Self-contained — no external service dependencies.
    /// </summary>
    private async Task<bool> HttpPostJsonAsync<T>(string url, T payload, string bearerToken, int timeoutSeconds = 60)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _navHttpClient.SendAsync(request).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("HTTP POST to {Url} failed with {StatusCode}: {Body}", url, response.StatusCode, body);
        }

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Normalizes a filter value to a list format expected by the UI.
    /// Handles boolean conversion and comma-separated multi-values.
    /// </summary>
    private static List<object> NormalizeFilterValue(object? value, string? filterType)
    {
        if (value == null)
            return new List<object>();

        if (filterType?.Equals("boolean", StringComparison.OrdinalIgnoreCase) == true)
        {
            var boolValue = value switch
            {
                bool b => b,
                string s when s.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
                string s when s.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
                _ => false
            };
            return new List<object> { boolValue };
        }

        var stringValue = value.ToString() ?? string.Empty;
        if (stringValue.Contains(','))
        {
            return stringValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => (object)v.Trim())
                .ToList();
        }

        return new List<object> { stringValue };
    }

    #endregion

    #region Page View Tool

    /// <summary>
    /// Allowed PageView metric config names. Must match the "Name" values in pageview-config.json.
    /// </summary>
    private static readonly HashSet<string> AllowedPageViewMetricNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_customers_page_view_link",
        "get_products_page_view_link",
        "get_programs_page_view_link"
    };

    [Description(@"Page View Tool: Retrieves page view navigation URL for customers view, products view, programs view with filtering conditions — no entity needed.
        
        This tool does not require Entity context, so no need to call SearchCustomerByNameAndWorkloadType/SearchProductByProductName beforehand. 

        WHEN TO USE:
        When User asks to fetch Customers with few filters. 
        When User asks to fetch Programs with few filters.
        When User asks to fetch Products with few filters.

        This Tool can provide below:
        Gets a list of customers provided the filtering conditions. This should NOT be used to fetch details about a specific customer. This should NOT be used without any filtering condition.    
        Gets the Programs provided the filtering conditions. This should NOT be used to fetch details about a specific program.
        Gets the Products provided the filtering conditions. This should NOT be used to fetch details about a specific product.
        
        IMPORTANT CONSTRAINTS:
        - Before calling this tool, you MUST call SearchMetricConfigs tool to get the correct metricConfigName and filter names.
        - Use EXACT metricName from SearchMetrics results - partial matches fail.
        - filters must use EXACT 'name' values from SearchMetrics (case-sensitive).

        Example usage:
        GetPageViewUrl('get_customers_page_view_link', 'Industry=Education,Country=USA|INDIA,#Subscriptions<=10000')
    ")]
    public async Task<CXOAgentResponse> GetPageViewUrl(
        [Description("The page view to query: 'get_customers_page_view_link', 'get_products_page_view_link', 'get_programs_page_view_link'")] string metricConfigName,
        [Description("KEY=VALUE pairs. ONLY use filter names from SearchMetrics results and comma separate for multiple filter names, | separate for multiple values. Example: 'Industry=Education', '#Subscriptions<=10000', 'Country=USA|INDIA'.")] string filters)
    {
        _logger.LogInformation("GetPageViewUrl | metricConfigName={MetricConfigName}, filters={Filters}", metricConfigName, filters);
        await NotifyAsync("🔗 Generating page view URL...");

        try
        {
            // Step 1: Validate metric config name is an allowed page view metric
            if (string.IsNullOrWhiteSpace(metricConfigName) ||
                !AllowedPageViewMetricNames.Contains(metricConfigName))
            {
                var allowed = string.Join(", ", AllowedPageViewMetricNames);
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = $"Invalid page view metric name '{metricConfigName}'. Allowed: [{allowed}]. Understand the user query and choose the right tool / metric config."
                };
            }

            if (_aspectToolsConfig is null)
            {
                await InitializeConfig();
            }

            // Step 2: Retrieve metric configuration
            var metricConfig = await GetMetricConfigAsync(metricConfigName);
            if (metricConfig == null)
            {
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    Response = $"Metric configuration '{metricConfigName}' not found. Please call SearchMetricConfigs and then SearchMetricConfigFilters first."
                };
            }

            // Step 3: Parse user-provided filters and build ViewFilters from FilterConfig
            var filtersArray = metricConfig[CxOAIConstants.Field_Filters] as JArray;
            var filterConfigs = filtersArray?.ToObject<List<FilterConfig>>()?
                .Where(f => f.IsActive).ToList() ?? new List<FilterConfig>();

            var appliedFilters = BuildViewFiltersFromFilterConfig(filters, filterConfigs);
            _logger.LogInformation("GetPageViewUrl | Built {Count} view filters from {ConfigCount} filter configs",
                appliedFilters.Count, filterConfigs.Count);

            // Step 4: Generate page view navigation URL
            var navigationUrl = await GetPageViewNavigationUrlAsync(metricConfig, appliedFilters);
            _logger.LogInformation("GetPageViewUrl | Generated navigation URL: {Url}", navigationUrl);

            var response = string.IsNullOrWhiteSpace(navigationUrl)
                ? "Failed to generate navigation URL."
                : $"Please navigate to <a target=\"_blank\" href={navigationUrl}> </a> to see the results.";

            await NotifyAsync("✅ Page view URL generated");
            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = response
            };
        }
        catch (Exception ex)
        {
            await NotifyAsync("❌ Unable to generate page view URL");
            _logger.LogError(ex, "GetPageViewUrl | Error for metricConfigName={MetricConfigName}", metricConfigName);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                Response = $"Unable to generate page view URL. {ex.Message}"
            };
            throw new ToolParameterException(JsonConvert.SerializeObject(response));
        }
    }

    /// <summary>
    /// Parses the user-provided filter string and builds ViewFilter objects from FilterConfig.
    /// Each matched FilterConfig is converted to a ViewFilter with the appropriate column type
    /// and the user-provided value/operator applied.
    /// Only returns ViewFilters for filters that have values in the input string.
    /// </summary>
    internal static List<ViewFilter> BuildViewFiltersFromFilterConfig(string filtersInput, List<FilterConfig> filterConfigs)
    {
        var viewFilters = new List<ViewFilter>();

        if (string.IsNullOrWhiteSpace(filtersInput) || filterConfigs.Count == 0)
            return viewFilters;

        var filterPairs = filtersInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pair in filterPairs)
        {
            var (name, op, value) = ParseFilterExpression(pair);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                continue;

            var filterConfig = filterConfigs.Find(f =>
                f.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(f.BackendFilterName) && f.BackendFilterName.Equals(name, StringComparison.OrdinalIgnoreCase)));

            if (filterConfig == null)
                continue;

            var columnName = filterConfig.BackendFilterName ?? filterConfig.Name;
            var columnType = DomainTempFilterConfig.GetUiColumnType(filterConfig.Type);

            var viewFilter = new ViewFilter
            {
                Name = filterConfig.Name,
                Column = columnName,
                ColumnType = columnType,
                ServerColumnType = columnType,
                DefaultSelectedValues = new List<object>(),
                SelectedValues = new List<object>(),
                ContainsSelectedValues = new List<string>(),
                FilterClause = "=="
            };

            if (string.Equals(op, "<", StringComparison.Ordinal) || string.Equals(op, "<=", StringComparison.Ordinal))
            {
                viewFilter.To = value;
                viewFilter.FilterClause = op;
            }
            else if (string.Equals(op, ">", StringComparison.Ordinal) || string.Equals(op, ">=", StringComparison.Ordinal))
            {
                viewFilter.From = value;
                viewFilter.FilterClause = op;
            }
            else if (string.Equals(op, "contains", StringComparison.OrdinalIgnoreCase))
            {
                viewFilter.ContainsSelectedValues = SplitPipeValues(value);
                viewFilter.FilterClause = "contains";
            }
            else if (string.Equals(op, "!=", StringComparison.Ordinal))
            {
                viewFilter.SelectedValues = SplitPipeValues(value).Cast<object>().ToList();
                viewFilter.FilterClause = "!=";
            }
            else
            {
                viewFilter.SelectedValues = SplitPipeValues(value).Cast<object>().ToList();
                viewFilter.FilterClause = "==";
            }

            viewFilters.Add(viewFilter);
        }

        return viewFilters;
    }

    /// <summary>
    /// Parses a single filter expression like 'Industry=Education', '#Subscriptions&lt;=10000', 'Country!=USA'.
    /// Returns (filterName, operator, value).
    /// </summary>
    internal static (string Name, string Operator, string Value) ParseFilterExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return (string.Empty, string.Empty, string.Empty);

        // Order matters: check two-char operators before single-char
        string[] operators = ["<=", ">=", "!=", "==", "<", ">", "="];

        foreach (var op in operators)
        {
            var idx = expression.IndexOf(op, StringComparison.Ordinal);
            if (idx > 0)
            {
                var name = expression[..idx].Trim();
                var value = expression[(idx + op.Length)..].Trim();
                var normalizedOp = op == "=" ? "==" : op;
                return (name, normalizedOp, value);
            }
        }

        // No operator found — treat as contains if it looks like 'Name:value'
        var colonIdx = expression.IndexOf(':');
        if (colonIdx > 0)
        {
            var name = expression[..colonIdx].Trim();
            var value = expression[(colonIdx + 1)..].Trim();
            return (name, "contains", value);
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// Splits pipe-separated values (e.g., 'USA|INDIA') into a list.
    /// </summary>
    private static List<string> SplitPipeValues(string value)
    {
        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    #endregion
}
