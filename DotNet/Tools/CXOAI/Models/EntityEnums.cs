using System.ComponentModel;

namespace CXOAI.Tools.Models;

/// <summary>
/// Azure workload types with their display descriptions.
/// Used by entity search to resolve free-text workload input.
/// </summary>
public enum WorkloadTypeEnum
{
    [Description("Vanguard")]
    Vanguard,

    [Description("SfMC Workloads")]
    SfMCWorkloads,

    [Description("S500")]
    S500,

    [Description("Quality Critical")]
    QualityCritical,

    [Description("Proactive Resilience")]
    ProactiveResilience,

    [Description("PRIMO Internal Test Workloads")]
    PRIMOInternalTestWorkloads,

    [Description("Mission Critical")]
    MissionCritical,

    [Description("Microsoft for Startups")]
    MicrosoftForStartups,

    [Description("Microsoft Internal Workloads")]
    MicrosoftInternalWorkloads,

    [Description("Majors")]
    Majors,

    [Description("ISV Resiliency Reviews")]
    ISVResiliencyReviews,

    [Description("HiPri")]
    HiPri,

    [Description("FTA Resiliency Pilot")]
    FTAResiliencyPilot,

    [Description("Cx Observe Test")]
    CxObserveTest,

    [Description("Core Mission Critical")]
    CoreMissionCritical,

    [Description("Cloud Native")]
    CloudNative,

    [Description("Cloud & AI Top")]
    CloudAndAITop,

    [Description("CXP Managed for Enterprise")]
    CXPManagedForEnterprise,

    [Description("CXP Managed (ISVs)")]
    CXPManagedISVs,

    [Description("CRE Review")]
    CREReview,

    [Description("CRE Adhoc")]
    CREAdhoc,

    [Description("Azure Priority 0 - Enhanced")]
    AzurePriority0Enhanced,

    [Description("Azure Priority 0")]
    AzurePriority0,

    [Description("Azure Core Critical")]
    AzureCoreCritical,

    [Description("Azure ACE / AED")]
    AzureACEAED,

    [Description("Atlas Monitored")]
    AtlasMonitored,

    [Description("AEM + HiPri")]
    AEMHiPri,

    [Description("AEM")]
    AEM,

    [Description("NONE")]
    None
}

/// <summary>
/// Azure program types with their CH URIs and display names.
/// Used by entity search to resolve free-text program input to CH URI identifiers.
/// </summary>
public enum ProgramTypeEnum
{
    [EnumMapping("ch:group:programs:fasttrack", "FastTrack")]
    FastTrack,

    [EnumMapping("ch:group:programs:ace", "Azure ACE / AED")]
    AzureACEAED,

    [EnumMapping("ch:group:programs:pulse", "Pulse")]
    Pulse,

    [EnumMapping("ch:group:programs:cxpmanaged", "CXP Managed for Enterprise")]
    CXPManagedForEnterprise,

    [EnumMapping("ch:group:programs:cxpmanagedisvs", "CXP Managed ISVs")]
    CXPManagedISVs,

    [EnumMapping("ch:group:programs:hipri", "HiPri")]
    HiPri,

    [EnumMapping("ch:group::21c00b83f462a787a5ed737efac9a119", "S500")]
    S500,

    [EnumMapping("ch:group:programs:hotdl", "HotDL")]
    HotDL,

    [EnumMapping("ch:group:programs:amp", "AMP")]
    AMP,

    [EnumMapping("ch:group::56d2ca217b948d98ecf3c12a41ecbfbf", "Azure Priority 0")]
    AzurePriority0,

    [EnumMapping("ch:group:programs:missioncritical", "Mission Critical")]
    MissionCritical,

    [EnumMapping("ch:group:programs:acx", "ACX Assessment")]
    ACXAssessment,

    [EnumMapping("ch:group:programs:sip", "SIP")]
    SIP,

    [EnumMapping("ch:group:support-plan:agreement:unified", "Unified")]
    Unified,

    [EnumMapping("ch:group:support-plan:agreement:classic", "Classic")]
    Classic,

    [EnumMapping("ch:group:support-plan:agreement:sfmc", "SFMC")]
    SFMC,

    [EnumMapping("ch:group:support-plan:agreement:arr", "ARR")]
    ARR,

    [EnumMapping("ch:group:programs:caitopcustomers", "Cloud & AI Top Customers")]
    CloudAndAITopCustomers,

    [EnumMapping("ch:group:programs:atlasmonitored", "Atlas Monitored")]
    AtlasMonitored,

    [EnumMapping("ch:group:programs:aem", "AEM")]
    AEM,

    [EnumMapping("ch:group:programs:aemhipri", "AEM + HiPri")]
    AEMHiPri,

    [EnumMapping("ch:group:programs:creadhoc", "CRE Adhoc")]
    CREAdhoc,

    [EnumMapping("ch:group:programs:crereview", "CRE Review")]
    CREReview,

    [EnumMapping("ch:group:programs:proactiveresiliency", "Proactive Resilience")]
    ProactiveResilience,

    [EnumMapping("ch:group:programs:azurepriorityzeroenhanced", "Azure Priority 0 - Enhanced")]
    AzurePriority0Enhanced,

    [EnumMapping("ch:group:programs:mfs", "Microsoft for Startups")]
    MicrosoftForStartups,

    [EnumMapping("ch:group:programs:qeicustomers", "QEI Customers")]
    QEICustomers,

    [EnumMapping("ch:group:programs:majors", "Majors")]
    Majors
}

/// <summary>
/// Maps a ProgramTypeEnum member to its CH URI and display name.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class EnumMappingAttribute : Attribute
{
    public string GroupChUri { get; }
    public string DisplayName { get; }

    public EnumMappingAttribute(string groupChUri, string displayName)
    {
        GroupChUri = groupChUri;
        DisplayName = displayName;
    }
}
