namespace CXOAI.Tools.Models;

/// <summary>
/// Domain-level settings for temp filter view generation.
/// These are constants derived from UI codebase - no need to store in config.
/// Source: AzureCXP-Eng-Observe-UX/observe/apps/{domain}view/layouts/*.json → filterKey.module
/// </summary>
public record DomainFilterSettings(
    string Category,
    string FilterKeyPrefix,
    string GroupKey
);

/// <summary>
/// Static configuration for domain-specific temp filter view settings.
/// Maps domain names to their corresponding UI filter categories and keys.
///
/// Filter key prefix is derived from UI layout's filterKey.module value:
/// filterKey: { app: "cxobserve", module: "SupportIncident", section: "PageFilter" }
/// => FilterKeyPrefix: "cxobserve_SupportIncident"
/// </summary>
public static class DomainTempFilterConfig
{
    private static readonly Dictionary<string, DomainFilterSettings> ViewPathMap = new(StringComparer.OrdinalIgnoreCase)
    {
        #region pageview customer,product,program

        ["allcustomers"] = new("CustomerFilteredView", "cxobserve_Customer", "oslo_global"),
        ["productsearch"] = new("ProductFilteredView", "cxobserve_productsearch", "oslo_global"),
        ["allprograms"] = new("ProgramFilteredView", "cxobserve_programs", "oslo_global"),

        #endregion

        #region Support

        ["support/Support%20Summary"] = new("SupportFilteredView", "cxobserve_Support", "oslo_global"),
        ["support/supportincident"] = new("SupportIncidentFilteredView", "cxobserve_SupportIncident", "oslo_global"),
        ["support/selfhelp"] = new("SelfHelpFilteredView", "cxobserve_SelfHelp", "oslo_global"),
        ["support/selfhelpsuccess"] = new("SelfHelpFilteredView", "cxobserve_SelfHelp", "oslo_global"),
        ["support/experience"] = new("CsatFilteredView", "cxobserve_Csat", "oslo_global"),
        ["support/efficiency"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["supportefficiency"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["support/nir"] = new("NIRFilteredView", "cxobserve_NIR", "oslo_global"),
        ["support/mbrdashboard"] = new("MBRFilteredView", "cxobserve_mbrDashboard", "oslo_global"),
        ["agreements"] = new("AgreementsFilteredView", "cxobserve_Agreements", "oslo_global"),

        #endregion

        #region Quality

        ["quality/summary"] = new("QualitySummaryFilteredView", "cxobserve_qualitysummary", "oslo_global"),
        ["quality/allincidents"] = new("QualityAllIncidentsFilteredView", "cxobserve_qualityallincidents", "oslo_global"),
        ["quality/outages"] = new("QualityIncidentsFilteredView", "cxobserve_Incidents", "oslo_global"),
        ["quality/customerreportedincidents"] = new("CustomerReportedIncidentsFilteredView", "cxobserve_customerreportedincidents", "oslo_global"),
        ["quality/actionrequired"] = new("ActionRequiredFilteredView", "cxobserve_actionrequired", "oslo_global"),
        ["quality/securityincidents"] = new("SecurityIncidentsFilteredView", "cxobserve_securityincidents", "oslo_global"),
        ["quality/maintenancenotifications"] = new("MaintenanceNotificationsFilteredView", "cxobserve_maintenancenotifications", "oslo_global"),
        ["quality/retirementnotifications"] = new("RetirementNotificationsFilteredView", "cxobserve_retirementnotifications", "oslo_global"),
        ["quality/repairitems"] = new("RepairItemsFilteredView", "cxobserve_repairitems", "oslo_global"),

        #endregion

        #region Consumption

        ["consumption/usage"] = new("ConsumptionUsageFilteredView", "cxobserve_ConsumptionUsage", "oslo_global"),
        ["consumption/revenue"] = new("ConsumptionRevenueFilteredView", "cxobserve_ConsumptionRevenue", "oslo_global"),
        ["consumption/compute"] = new("ConsumptionComputeFilteredView", "cxobserve_ConsumptionCompute", "oslo_global"),
        ["consumption/storage"] = new("ConsumptionStorageFilteredView", "cxobserve_ConsumptionStorage", "oslo_global"),
        ["consumption/sqldb"] = new("ConsumptionSQLDBFilteredView", "cxobserve_Consumptionsqldb", "oslo_global"),

        #endregion

        #region Customer

        ["customer/customerApplicationProfile"] = new("CustomerApplicationProfileFilteredView", "Oslo_customerApplicationProfile", "oslo_global"),

        #endregion

        #region Engagement

        ["gethelp"] = new("GetHelpFilteredView", "cxobserve_GetHelp", "oslo_global"),
        ["engagements/Engagement%20Summary"] = new("EngagementSummaryFilteredView", "cxobserve_engagementsummary", "oslo_global"),
        ["engagements/Engagement%20feedback"] = new("EngagementFeedbackFilteredView", "cxobserve_engagementfeedback", "oslo_global"),
        ["engagements/Azure%20CXP/ACE%20SIP"] = new("EngagementACESIPFilteredView", "cxobserve_engagementace", "oslo_global"),
        ["engagements/Azure%20CXP/ACP"] = new("EngagementACPFilteredView", "cxobserve_engagementacp", "oslo_global"),
        ["engagements/Azure%20CXP/ACX%20Assessment"] = new("EngagementACXAssessmentFilteredView", "cxobserve_engagementacxassessment", "oslo_global"),
        ["engagements/Azure%20CXP/AEM"] = new("EngagementAEMFilteredView", "cxobserve_engagementame", "oslo_global"),
        ["engagements/Azure%20CXP/AMM"] = new("EngagementAMMFilteredView", "cxobserve_engagementamp", "oslo_global"),
        ["engagements/Azure%20CXP/Azure%20Priority%200"] = new("EngagementAzurePriority0FilteredView", "cxobserve_engagementlifeandsafety", "oslo_global"),
        ["engagements/Azure%20CXP/CRE%20Review"] = new("EngagementCREReviewFilteredView", "cxobserve_engagementcrereview", "oslo_global"),
        ["engagements/Azure%20CXP/CXP%20Managed%20For%20Enterprise"] = new("EngagementCXPManagedForEnterpriseFilteredView", "cxobserve_engagementcxpmanagedforenter", "oslo_global"),
        ["engagements/Azure%20CXP/CXP%20Managed%20For%20ISVs"] = new("EngagementCXPManagedForISVsFilteredView", "cxobserve_engagementcxpmanagedforisv", "oslo_global"),
        ["engagements/Azure%20CXP/FastTrack"] = new("EngagementFastTrackFilteredView", "cxobserve_engagementsfasttrack", "oslo_global"),
        ["engagements/Azure%20CXP/HiPri"] = new("EngagementHiPriFilteredView", "cxobserve_engagementhipri", "oslo_global"),
        ["engagements/Azure%20CXP/ISV"] = new("EngagementISVFilteredView", "cxobserve_engagementisv", "oslo_global"),
        ["engagements/Azure%20CXP/Pulse"] = new("EngagementPulseFilteredView", "cxobserve_engagementpulse", "oslo_global"),
        ["engagements/Azure%20CXP/SIP"] = new("EngagementSIPFilteredView", "cxobserve_engagementsip", "oslo_global"),

        #endregion

        #region Reliability

        ["reliability/Resiliency%20Insights/API%20Management"] = new("ApiManagementFilteredView", "cxobserve_ApiManagement", "oslo_global"),
        ["reliability/reliability/Resiliency%20Insights/ApplicationGateways"] = new("ApplicationGatewaysFilteredView", "cxobserve_ApplicationGateways", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Advisor/Overview"] = new("AzureAdvisorFilteredView", "cxobserve_AzureAdvisor", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Backup"] = new("AzureBackupFilteredView", "cxobserve_AzureBackup", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Batch"] = new("AzureBatchFilteredView", "cxobserve_AzureBatch", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Cache%20for%20Redis"] = new("AzureCacheForRedisFilteredView", "cxobserve_AzureCacheForRedis", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Container%20Registry"] = new("AzureContainerRegistryFilteredView", "cxobserve_Azure ContainerRegistry", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Cosmos%20DB"] = new("AzureCosmosDbFilteredView", "cxobserve_AzureCosmosDb", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Kubernetes%20Service"] = new("AzureKubernetesServiceFilteredView", "cxobserve_AzureKubernetesService", "oslo_global"),
        ["reliability/Resiliency%20Insights/AzureStorageAccountt"] = new("AzureStorageAccountFilteredView", "cxobserve_AzureStorageAccount", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20Site%20Recovery"] = new("AzureSiteRecoveryFilteredView", "cxobserve_AzureSiteRecovery", "oslo_global"),
        ["reliability/Resiliency%20Insights/Azure%20SQL%20DB"] = new("AzureSqlDBResiliencyFilteredView", "cxobserve_AzureSqlDBResiliency", "oslo_global"),
        ["reliability/Resiliency%20Insights/Compute%20and%20Storage%20Utilization"] = new("ComputeUtilizationFilteredView", "cxobserve_ComputeUtilization", "oslo_global"),
        ["reliability/Resiliency%20Insights/Express%20Routes"] = new("ExpressRoutesFilteredView", "cxobserve_ExpressRoutes", "oslo_global"),
        ["reliability/Resiliency%20Insights/Service%20Alerts%20Setup"] = new("ServiceHelthAlertSetupFilteredView", "cxobserve_ServiceHelthAlertSetup", "oslo_global"),
        ["reliability/StorageResiliency"] = new("StorageResiliencyFilteredView", "cxobserve_StorageResiliency", "oslo_global"),
        ["reliability/Resiliency%20Insights/Zone%20Resiliency"] = new("ZoneResiliencyFilteredView", "cxobserve_ZoneResiliencyPage", "oslo_global"),
        ["reliability/Resiliency%20Insights/RaaS%20Report"] = new("RaaSReportFilteredView", "cxobserve_RaaSReport", "oslo_global"),
        ["reliability/Azure%20Telemetry%20Insights"] = new("AzureTelemetryInsightsFilteredView", "cxobserve_AzureTelemetryInsights", "oslo_global"),
        ["reliability/AIR%20BP"] = new("AIRBPFilteredView", "cxobserve_AIRBP", "oslo_global"),
        ["reliability/AIR%20Reboot"] = new("AIRRebootFilteredView", "cxobserve_AIRReboot", "oslo_global"),
        ["reliability/ARM%20Throttling"] = new("ArmThrottlingFilteredView", "cxobserve_ArmThrottling", "oslo_global"),
        ["reliability/Azure%20Storage%20Resource"] = new("AzureStorageResourceHealthFilteredView", "cxobserve_AzureStorageResourceHealth", "oslo_global"),
        ["reliability/CFR"] = new("CFRFilteredView", "cxobserve_CFR", "oslo_global"),
        ["reliability/Storage%20Availability%20Drop"] = new("StorageAvailabilityDropFilteredView", "cxobserve_StorageAvailabilityDrop", "oslo_global"),
        ["reliability/ControlPlaneOperation"] = new("ControlPlaneOperationFilteredView", "cxobserve_ControlPlaneOperation", "oslo_global"),
        ["reliability/ControlPlaneFailureRCA"] = new("ControlPlaneFailureRCAFilteredView", "cxobserve_ControlPlaneFailureRCA", "oslo_global"),
        ["reliability/ControlPlaneLatencyAnalysis"] = new("ControlPlaneLatencyAnalysisFilteredView", "cxobserve_ControlPlaneLatencyAnalysis", "oslo_global"),
        ["reliability/ControlPlanePreventiveRecommendations"] = new("ControlPlanePreventiveRecommendationsFilteredView", "cxobserve_ControlPlanePreventiveRecommendations", "oslo_global"),

        #endregion

        #region Product

        ["products/reliability/AIR%20Reboot"] = new("AIRRebootProductFilteredView", "CxObserve_Product-Summary", "oslo_global"),
        ["products/reliability/AIR%20BP"] = new("AIRBPProductFilteredView", "CxObserve_Product-Summary", "oslo_global"),
        ["products/productsummary"] = new("ProductSummaryFilteredView", "cxobserve_Product-Summary", "oslo_global"),
        ["products/productsummaryazure"] = new("ProductSummaryAzureFilteredView", "cxobserve_Product-Summary", "oslo_global"),
        ["products/productairreboot"] = new("ProductAirRebootFilteredView", "cxobserve_Product-Summary", "oslo_global"),
        ["products/productairbp"] = new("ProductAirBPFilteredView", "cxobserve_Product-Summary", "oslo_global"),
        ["products/consumption/usage"] = new("ConsumptionUsageProductFilteredView", "cxobserve_ConsumptionUsageProduct", "oslo_global"),
        ["products/consumption/revenue"] = new("ConsumptionRevenueProductFilteredView", "cxobserve_ConsumptionRevenueProduct", "oslo_global"),
        ["products/quality/summary"] = new("QualitySummaryProductFilteredView", "cxobserve_qualitysummaryproduct", "oslo_global"),
        ["products/quality/outages"] = new("OutagesProductFilteredView", "cxobserve_outagesproduct", "oslo_global"),
        ["products/quality/customerreportedincidents"] = new("CustomerReportedIncidentsProductFilteredView", "cxobserve_customerreportedincidentsproduct", "oslo_global"),
        ["products/quality/allincidents"] = new("QualityAllIncidentsProductFilteredView", "cxobserve_qualityallincidentsproduct", "oslo_global"),
        ["products/support/selfHelp"] = new("SelfHelpProductFilteredView", "cxobserve_SelfHelpProduct", "oslo_global"),
        ["products/support/supportincident"] = new("SupportIncidentProductFilteredView", "cxobserve_SupportIncidentProduct", "oslo_global"),
        ["products/support/experience"] = new("CsatProductFilteredView", "cxobserve_csatProduct", "oslo_global"),
        ["products/support/efficiency"] = new("EfficiencyProductFilteredView", "cxobserve_efficiencyProduct", "oslo_global"),
        ["products/support/nir"] = new("NIRProductFilteredView", "cxobserve_NIRProduct", "oslo_global"),
        ["products/engfeedbacks"] = new("EngFeedbacksFilteredView", "cxobserve_engfeedbacks", "oslo_global"),

        #endregion
    };

    private static readonly Dictionary<string, DomainFilterSettings> DomainMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Support"] = new("SupportFilteredView", "cxobserve_Support", "oslo_global"),
        ["Quality"] = new("QualitySummaryFilteredView", "cxobserve_qualitysummary", "oslo_global"),
        ["Reliability"] = new("CustomerFilteredView", "cxobserve_Customer", "cxobserve_Customer_customFilters"),
        ["Consumption"] = new("CustomerFilteredView", "cxobserve_Customer", "cxobserve_Customer_customFilters"),
        ["Customer"] = new("CustomerFilteredView", "cxobserve_Customer", "cxobserve_Customer_customFilters"),
        ["Product"] = new("ProductFilteredView", "cxobserve_productsearch", "cxobserve_productsearch_customFilters"),
        ["Program"] = new("ProgramFilteredView", "cxobserve_programs", "cxobserve_programs_customFilters"),
    };

    private static readonly Dictionary<string, DomainFilterSettings> AspectMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["convergence:incidents"] = new("SupportIncidentFilteredView", "cxobserve_SupportIncident", "oslo_global"),
        ["convergence:csat"] = new("CsatFilteredView", "cxobserve_Csat", "oslo_global"),
        ["convergence:selfhelp"] = new("SelfHelpFilteredView", "cxobserve_SelfHelp", "oslo_global"),
        ["convergence:ttms"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["convergence:cpt"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["convergence:tmpi"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["convergence:sesat"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["convergence:customersupporteffort"] = new("EfficiencyFilteredView", "cxobserve_TTMSCustomer", "oslo_global"),
        ["support:SupportCases"] = new("SupportFilteredView", "cxobserve_Support", "oslo_global"),
        ["cxobserve:CxObserveIncidents"] = new("QualitySummaryFilteredView", "cxobserve_qualitysummary", "oslo_global"),
        ["cxobserve:CxObserveTTxPercentilesTrend"] = new("QualitySummaryFilteredView", "cxobserve_qualitysummary", "oslo_global"),
    };

    /// <summary>
    /// Gets the filter settings based on view path (highest priority), aspect URL (medium), or domain (fallback).
    /// </summary>
    public static DomainFilterSettings? GetSettings(string domain, string pageType, string? aspectUrl = null, string? viewPath = null)
    {
        if (!string.IsNullOrEmpty(viewPath))
        {
            var normalizedViewPath = Uri.UnescapeDataString(viewPath).Trim('/');
            if (ViewPathMap.TryGetValue($"{pageType}/{normalizedViewPath}", out var viewSettings))
                return viewSettings;
            if (ViewPathMap.TryGetValue(normalizedViewPath, out viewSettings))
                return viewSettings;
        }

        if (!string.IsNullOrEmpty(aspectUrl))
        {
            var aspectType = ExtractAspectType(aspectUrl);
            if (!string.IsNullOrEmpty(aspectType) && AspectMap.TryGetValue(aspectType, out var aspectSettings))
                return aspectSettings;
        }

        return DomainMap.TryGetValue(domain, out var settings) ? settings : null;
    }

    private static string? ExtractAspectType(string aspectUrl)
    {
        var urlWithoutQuery = aspectUrl.Split('?')[0];
        if (!urlWithoutQuery.StartsWith("ch:aspect:", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = urlWithoutQuery.Substring("ch:aspect:".Length).Split(':');
        if (parts.Length >= 2)
            return $"{parts[0]}:{parts[1]}";

        return parts.Length >= 1 ? parts[0] : null;
    }

    /// <summary>
    /// Derives UI column type from config filter type.
    /// </summary>
    public static int GetUiColumnType(string? configType) => configType?.ToLowerInvariant() switch
    {
        "boolean" => 2,
        "integer" => 11,
        "number" => 11,
        "date" => 3,
        "array" => 18,
        _ => 18
    };

    /// <summary>
    /// Determines page type from entity ID for URL construction.
    /// </summary>
    public static string GetPageType(string entityId) => entityId switch
    {
        var e when e.StartsWith("ch:product", StringComparison.OrdinalIgnoreCase) => "products",
        var e when e.StartsWith("ch:group", StringComparison.OrdinalIgnoreCase) => "programs",
        _ => "customers"
    };

    public static IEnumerable<string> GetSupportedDomains() => DomainMap.Keys;
}
