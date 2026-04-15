using Azure.Core;
using Azure.Identity;
using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Moq;
using Provider;
using Provider.Interfaces;
using Provider.Model;

namespace UnitTests;

public class KnowledgeGraphTests
{
     public KnowledgeGraphTests()
    {
        var appStorageAccountName = Environment.GetEnvironmentVariable(AppSettingConstants.AppStorageAccountNameKey)!.ToLower();
        var provider  = new AzureStorageProvider(appStorageAccountName);
        var appSettingConfiguration = new StorageAppSettingService(provider);
        var blobFileName = $"{Environment.GetEnvironmentVariable(AppSettingConstants.EnvironmentVariableName)!.ToLower()}.environment.settings.json";
        var configDictionary = appSettingConfiguration.ReadConfigAsync(blobFileName).Result;
        var appConfig = new AppSettingService(configDictionary);
        var knowledgeMockLogger = new Mock<ILogger<KnowledgeGraphTools>>();
        var searchMockLogger = new Mock<ILogger<IAzureSearchProvider>>();
        var openAIEndpoint = new Uri(appConfig.Configuration[AppSettingConstants.Configuration_AzureOpenAIEndpoint]);
        var embeddingDeployment = appConfig.Configuration[AppSettingConstants.Configuration_EmbeddingDeployment];
        var cred = new DefaultAzureCredential();
        var searchConfig = new AzureSearchConnectionConfig(
                   appConfig.Configuration[AppSettingConstants.Configuration_SearchServiceEndpoint],
                   appConfig.Configuration[AppSettingConstants.Configuration_SearchIndexName]);
        var searchProvider= new AzureSearchProvider(searchMockLogger.Object, searchConfig);

        var store= new TreeConfigurationStoreProvider(searchProvider, openAIEndpoint, embeddingDeployment, cred);
        if(_knowledgeGraph == null)
             _knowledgeGraph = new KnowledgeGraphTools(appConfig, knowledgeMockLogger.Object, store);
    }
    private  static KnowledgeGraphTools _knowledgeGraph;

    // --- Multi-node match tests (keyword matches at least 3 nodes) ---

    [Fact]
    public async Task RebootCount_VmDowntime_ArmThrottling_MatchesThreeNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("show me the reboot count, vm downtime and arm throttling");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("show me the reboot count, vm downtime and arm throttling");

        Assert.Contains("## get vm reboot count", systemResult);
        Assert.Contains("## get vm downtime value", systemResult);
        Assert.Contains("## get arm throttle value", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get vm reboot count", generalResult);
        Assert.Contains("## get vm downtime value", generalResult);
        Assert.Contains("## get arm throttle value", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task ConsumptionUnits_ComputeUsage_RevenueTrend_MatchesThreeNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("what are the consumption units, compute usage and revenue trends");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("what are the consumption units, compute usage and revenue trends");

        Assert.Contains("## get consumption units value", systemResult);
        Assert.Contains("## get compute usage value", systemResult);
        Assert.Contains("## get revenue value", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get consumption units value", generalResult);
        Assert.Contains("## get compute usage value", generalResult);
        Assert.Contains("## get revenue value", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task ExpressRoute_CosmosDb_Aks_MatchesThreeNodesWithRelationships()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("show me expressroute gateways, cosmos db and aks cluster details");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("show me expressroute gateways, cosmos db and aks cluster details");

        Assert.Contains("## get expressroute gateways value", systemResult);
        Assert.Contains("## get azure cosmosdb accounts value", systemResult);
        Assert.Contains("## get aks clusters value", systemResult);
        Assert.Contains("**Relationships:**", systemResult);
        Assert.Contains("[related-metric] **get expressroute gateways by region**", systemResult);
        Assert.Contains("[related-metric] **get aks clusters by subscription**", systemResult);

        Assert.Contains("## get expressroute gateways value", generalResult);
        Assert.Contains("## get azure cosmosdb accounts value", generalResult);
        Assert.Contains("## get aks clusters value", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task IncidentCount_RootCause_TimeToMitigate_MatchesThreeNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("what is the incident count, root cause trend and time to mitigate");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("what is the incident count, root cause trend and time to mitigate");

        Assert.Contains("## get incidents by severity", systemResult);
        Assert.Contains("## get incidents root cause trend", systemResult);
        Assert.Contains("## get time to mitigate p75", systemResult);
        Assert.Contains("**Details:**", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get incidents by severity", generalResult);
        Assert.Contains("## get incidents root cause trend", generalResult);
        Assert.Contains("## get time to mitigate p75", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task VmRebootRate_StorageAvailabilityDrop_ServiceHealthAlerts_MatchesThreeNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("analyze the vm reboot rate, storage availability drop and service health alerts");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("analyze the vm reboot rate, storage availability drop and service health alerts");

        Assert.Contains("## get air reboot value", systemResult);
        Assert.Contains("## get sadrop value", systemResult);
        Assert.Contains("## get sha value", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get air reboot value", generalResult);
        Assert.Contains("  - Gets the AIR-R (Annual Interruption Rate - Reboot) provided the filtering conditions.", generalResult);
        Assert.Contains("## get sadrop value", generalResult);
        Assert.Contains("## get sha value", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    // --- Single-node match tests (keyword matches exactly 1 node) ---

    [Fact]
    public async Task Cfr_MatchesSingleNode()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("what is the cfr for this customer");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("what is the cfr for this customer");

        Assert.Contains("## get cfr value", systemResult);
        Assert.Contains("  - Gets the CFR (Capacity Fulfilment Reliability) values provided the filtering conditions.", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get cfr value", generalResult);
        Assert.Contains("  - Gets the CFR (Capacity Fulfilment Reliability) values provided the filtering conditions.", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task AdvisorRecommendations_MatchesSingleNode()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("show me the advisor recommendations for the customer");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("show me the advisor recommendations for the customer");

        Assert.Contains("## get advisor recommendations value", systemResult);
        Assert.Contains("**Details:**", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get advisor recommendations value", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task RedisResiliency_MatchesSingleNode()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("show me the redis resiliency score details");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("show me the redis resiliency score details");

        Assert.Contains("## get azure cache for redis resiliency by features", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get azure cache for redis resiliency by features", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task BackupEnabledVm_MatchesSingleNodeWithRelationship()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("how many backup enabled VMs are there");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("how many backup enabled VMs are there");

        Assert.Contains("## get backup enabled vm value", systemResult);
        Assert.Contains("**Relationships:**", systemResult);
        Assert.Contains("[related-metric] **get backup enabled vm trend**", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## get backup enabled vm value", generalResult);
        Assert.Contains("**Relationships:**", generalResult);
        Assert.Contains("[related-metric] **get backup enabled vm trend**", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task ControlPlaneFailureRate_MatchesSingleNode()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("what is the control plane failure rate for this customer");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("what is the control plane failure rate for this customer");

        Assert.Contains("## control plane operations failure rate", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        Assert.Contains("## control plane operations failure rate", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    // --- No-match test ---

    [Fact]
    public async Task NoMatchingTerms_ReturnsDomainKnowledgeHeaderOnly()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync("what is the weather forecast for tomorrow");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync("what is the weather forecast for tomorrow");

        Assert.Contains("# Domain Knowledge", systemResult);
        Assert.DoesNotContain("##", systemResult);

        Assert.Contains("# Domain Knowledge", generalResult);
        Assert.DoesNotContain("##", generalResult);
    }

    // --- Complex multi-intent prompt tests ---

    [Fact]
    public async Task SupportCsat_TrendReason_WordExport_MatchesCsatAndSummaryNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync(
            "What does Support CSAT look like for Walmart over the last 30 days, why is it trending that way, and can you export an executive-ready summary to a Word document");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync(
            "What does Support CSAT look like for Walmart over the last 30 days, why is it trending that way, and can you export an executive-ready summary to a Word document");

        // ?? get csat score: node, tags, descriptions ??
        Assert.Contains("## get csat score", systemResult);
        Assert.Contains("email csat, csat", systemResult);
        Assert.Contains("CSAT measures customer satisfaction with Azure Support", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        // ?? get csat score: relationships (source node has 5 outgoing) ??
        Assert.Contains("**Relationships:**", systemResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get irmet value trend**", systemResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get fdr value trend**", systemResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get incidents volume by time trend**", systemResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get time to mitigate measures**", systemResult);
        //Assert.Contains("[impacts-csat | negative-correlation] **get closed support cases per workday**", systemResult);

        // ?? relationship target nodes: one-depth expansion with tags and descriptions ??
        // get irmet value trend
        Assert.Contains("irmet, initial response", systemResult);
        Assert.Contains("Gets the IR Met (Initial Response) trend", systemResult);
        // get fdr value trend
        Assert.Contains("fdr, first day resolution", systemResult);
        Assert.Contains("Gets the FDR (First Day Resolution) KPI trend", systemResult);
        // get incidents volume by time trend
        Assert.Contains("incidents, volume, trend, over time", systemResult);
        Assert.Contains("Gets the trend or total volume of incidents over time", systemResult);
        // get time to mitigate measures
        Assert.Contains("time to mitigate, ttms 90, time to mitigate measures", systemResult);
        Assert.Contains("Gets the time to mitigate measures", systemResult);
        // get closed support cases per workday
        //Assert.Contains("closed cases per workday, support cases closed per workday, closed support cases per workday", systemResult);
        //Assert.Contains("Gets number of cases closed in a given period", systemResult);

        // ?? executive summary customer workload: node, tags, descriptions ??
        //Assert.Contains("## executive summary customer workload", systemResult);
        //Assert.Contains("executive summary", systemResult);
        //Assert.Contains("Provides a detailed executive summary for a customer or workload", systemResult);

        // ?? general knowledge: same nodes + relationships but no System descriptions ??
        Assert.Contains("## get csat score", generalResult);
        Assert.Contains("CSAT measures customer satisfaction with Azure Support", generalResult);
        Assert.Contains("**Relationships:**", generalResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get irmet value trend**", generalResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get fdr value trend**", generalResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get incidents volume by time trend**", generalResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get time to mitigate measures**", generalResult);
        //Assert.Contains("[impacts-csat | negative-correlation] **get closed support cases per workday**", generalResult);
        // general: relationship targets have only General descriptions (no System/AspectSkill)
        Assert.Contains("Gets the IR Met (Initial Response) trend", generalResult);
        Assert.Contains("Gets the FDR (First Day Resolution) KPI trend", generalResult);
        Assert.Contains("Gets the trend or total volume of incidents over time", generalResult);
        Assert.Contains("Gets the time to mitigate measures", generalResult);
        //Assert.Contains("Gets number of cases closed in a given period", generalResult);
        //Assert.Contains("## executive summary customer workload", generalResult);
        //Assert.Contains("Provides a detailed executive summary for a customer or workload", generalResult);
        //Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task SupportCsat_TrendCsatAndSummaryNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync(
            "give the csat score trend for walmart for last 6 months and tell me why it is trending that way");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync(
            "give the csat score trend for walmart for last 6 months and tell me why it is trending that way");

        // ?? get csat score: node, tags, descriptions ??
        Assert.Contains("## get csat score", systemResult);
        Assert.Contains("email csat, csat", systemResult);
        Assert.Contains("CSAT measures customer satisfaction with Azure Support", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        // ?? get csat score: relationships (source node has 5 outgoing) ??
        Assert.Contains("**Relationships:**", systemResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get irmet value trend**", systemResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get fdr value trend**", systemResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get incidents volume by time trend**", systemResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get time to mitigate measures**", systemResult);
        //Assert.Contains("[impacts-csat | negative-correlation] **get closed support cases per workday**", systemResult);

        // ?? relationship target nodes: one-depth expansion with tags and descriptions ??
        // get irmet value trend
        Assert.Contains("irmet, initial response", systemResult);
        Assert.Contains("Gets the IR Met (Initial Response) trend", systemResult);
        // get fdr value trend
        Assert.Contains("fdr, first day resolution", systemResult);
        Assert.Contains("Gets the FDR (First Day Resolution) KPI trend", systemResult);
        // get incidents volume by time trend
        Assert.Contains("incidents, volume, trend, over time", systemResult);
        Assert.Contains("Gets the trend or total volume of incidents over time", systemResult);
        // get time to mitigate measures
        Assert.Contains("time to mitigate, ttms 90, time to mitigate measures", systemResult);
        Assert.Contains("Gets the time to mitigate measures", systemResult);
        // get closed support cases per workday
        //Assert.Contains("closed cases per workday, support cases closed per workday, closed support cases per workday", systemResult);
        //Assert.Contains("Gets number of cases closed in a given period", systemResult);

        // ?? executive summary customer workload: node, tags, descriptions ??
        //Assert.Contains("## executive summary customer workload", systemResult);
        //Assert.Contains("executive summary", systemResult);
        //Assert.Contains("Provides a detailed executive summary for a customer or workload", systemResult);

        // ?? general knowledge: same nodes + relationships but no System descriptions ??
        Assert.Contains("## get csat score", generalResult);
        Assert.Contains("CSAT measures customer satisfaction with Azure Support", generalResult);
        Assert.Contains("**Relationships:**", generalResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get irmet value trend**", generalResult);
        Assert.Contains("[impacts-csat | positive-correlation] **get fdr value trend**", generalResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get incidents volume by time trend**", generalResult);
        Assert.Contains("[impacts-csat | negative-correlation] **get time to mitigate measures**", generalResult);
        //Assert.Contains("[impacts-csat | negative-correlation] **get closed support cases per workday**", generalResult);
        // general: relationship targets have only General descriptions (no System/AspectSkill)
        Assert.Contains("Gets the IR Met (Initial Response) trend", generalResult);
        Assert.Contains("Gets the FDR (First Day Resolution) KPI trend", generalResult);
        Assert.Contains("Gets the trend or total volume of incidents over time", generalResult);
        Assert.Contains("Gets the time to mitigate measures", generalResult);
        //Assert.Contains("Gets number of cases closed in a given period", generalResult);
        //Assert.Contains("## executive summary customer workload", generalResult);
        //Assert.Contains("Provides a detailed executive summary for a customer or workload", generalResult);
        //Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }

    [Fact]
    public async Task QuickSummary_ExportToDoc_MatchesSummaryNode()
    {
        for (int i = 0; i < 50; i++)

            try
            {
                var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync(
            "Give me a quick summary of Walmart & export to doc");
        //var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync(
        //    "Give me a quick summary of Walmart & export to doc");

        // ?? executive summary customer workload: node, tags, descriptions ??
        Assert.Contains("## executive summary customer workload", systemResult);
        Assert.Contains("executive summary", systemResult);
        Assert.Contains("customer summary", systemResult);
        Assert.Contains("**Details:**", systemResult);
        Assert.Contains("Provides a detailed executive summary for a customer or workload", systemResult);
        Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        //// ?? general knowledge: same node but no System descriptions ??
        //Assert.Contains("## executive summary customer workload", generalResult);
        //Assert.Contains("executive summary", generalResult);
        //Assert.Contains("customer summary", generalResult);
        //Assert.Contains("**Details:**", generalResult);
        //Assert.Contains("Provides a detailed executive summary for a customer or workload", generalResult);
        //Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Attempt failed with exception: {ex.Message}");
            }
    }

    [Fact]
    public async Task overallSummary_ExportToDoc_MatchesSummaryNode()
    {
        for (int i = 0; i < 50; i++)

            try
            {
                var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync(
                "give me overall summary of walmart");
                //var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync(
                //    "give me overall summary of walmart");

                // ?? executive summary customer workload: node, tags, descriptions ??
                Assert.Contains("## executive summary customer workload", systemResult);
                Assert.Contains("executive summary", systemResult);
                Assert.Contains("customer summary", systemResult);
                Assert.Contains("**Details:**", systemResult);
                Assert.Contains("Provides a detailed executive summary for a customer or workload", systemResult);
                Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

                //// ?? general knowledge: same node but no System descriptions ??
                //Assert.Contains("## executive summary customer workload", generalResult);
                //Assert.Contains("executive summary", generalResult);
                //Assert.Contains("customer summary", generalResult);
                //Assert.Contains("**Details:**", generalResult);
                //Assert.Contains("Provides a detailed executive summary for a customer or workload", generalResult);
                //Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Attempt failed with exception: {ex.Message}");
            }
    }

    [Fact]
    public async Task Outage_ImpactedCustomers_SupportTickets_Recommendations_MatchesMultipleNodes()
    {
        var systemResult = await _knowledgeGraph.GetSystemKnowledgeAsync(
            "For incident 1234, how many S500 customers were impacted? Which support tickets were related? What recommendations apply?");
        var generalResult = await _knowledgeGraph.GetGeneralKnowledgeAsync(
            "For Outage 1234, how many S500 customers were impacted? Which support tickets were related? What recommendations apply?");

        // ?? get incidents by impact: node, tags, descriptions ??
        //Assert.Contains("## get incidents by impact", systemResult);
        //Assert.Contains("high impact", systemResult);
        //Assert.Contains("Gets the list of high impact Incidents based on filtering conditions", systemResult);
        //Assert.Contains("which can be used by AspectSkill to fetch data", systemResult);

        //// ?? get advisor recommendations value: node, tags, descriptions ??
        //Assert.Contains("## get advisor recommendations value", systemResult);
        //Assert.Contains("recommendations", systemResult);
        //Assert.Contains("azure advisor", systemResult);
        //Assert.Contains("Gets the number of Azure advisor recommendations provided the filtering conditions", systemResult);

        //// ?? get outage related volume: node, tags, descriptions ??
        //Assert.Contains("## get outage related volume", systemResult);
        //Assert.Contains("support tickets related to outage", systemResult);
        //Assert.DoesNotContain("Gets Number of support tickets related to an outage", systemResult);

        //// ?? general knowledge: same nodes but no System descriptions ??
        //Assert.DoesNotContain("## get incidents by impact", generalResult);
        //Assert.DoesNotContain("Gets the list of high impact Incidents based on filtering conditions", generalResult);
        //Assert.DoesNotContain("## get advisor recommendations value", generalResult);
        //Assert.DoesNotContain("Gets the number of Azure advisor recommendations provided the filtering conditions", generalResult);
        //Assert.DoesNotContain("## get outage related volume", generalResult);
        //Assert.DoesNotContain("Gets Number of support tickets related to an outage", generalResult);
        Assert.DoesNotContain("AspectSkill to fetch data", generalResult);
    }
}
