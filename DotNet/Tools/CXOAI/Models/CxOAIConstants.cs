namespace CXOAI.Tools.Models;

/// <summary>
/// Constants used by the CxOAI Aspect and Entity Search tools.
/// Ported from old repo's AppService.CxOAIConstants.
/// </summary>
public static class CxOAIConstants
{
    // Configuration store keys
    public const string ConfigComponent_ToolConfiguration = "ToolConfiguration";
    public const string ConfigComponent_AspectConfiguration = "AspectConfiguration";
    public const string ConfigName_EnvironmentSettings = "AspectTool-EnvironmentSettings";
    public const string ConfigName_GlobalFilters = "AspectTool-GlobalFilters";

    // Domain keys for AspectApiConfig dictionary
    public const string Domain_Customer = "customer";
    public const string Domain_Product = "product";
    public const string Domain_Support = "support";

    // Insights API URL templates
    // {0} = BaseUrl, {1} = entityUri, {2} = aspectPath
    public const string InsightsApiUrl = "{0}/api/Insights/{1}/aspects/{2}";

    // Insights API path format (without base URL)
    // {0} = entityUri, {1} = aspectUrl
    public const string InsightsApiPathFormat = "/api/Insights/{0}/aspects/{1}";

    // CH URI prefixes
    public const string ChUriProductPrefix = "ch:product::id:";
    public const string ChUriCustomerPrefix = "ch:customer::tpid:";

    // Configuration keys
    public const string Configuration_AspectsDetailsMap = "aspectsDetailsMap";
    public const string Configuration_MiscUrls = "miscurls";

    // JSON field names for metric config navigation
    public const string Field_Name = "Name";
    public const string Field_Domain = "Domain";
    public const string Field_Filters = "Filters";
    public const string Field_DataSource = "DataSource";
    public const string Field_SystemUrl = "SystemUrl";
    public const string Field_AdditionalMetadata = "AdditionalMetadata";
    public const string Field_MetricViewPath = "MetricViewPath";
    public const string Field_MetricUIComponentMap = "MetricUIComponentMap";
    public const string Field_TempFilterViewEnabled = "TempFilterViewEnabled";
    public const string Field_AgentPrompt = "AgentPrompt";

    // JSON field names for metric config structure
    public const string Field_Keywords = "Keywords";
    public const string Field_SupportedEntityTypes = "SupportedEntityTypes";
    public const string Field_SupportedEntities = "SupportedEntities";
    public const string Field_SelectGroupBy = "SelectGroupBy";
    public const string Field_Select = "Select";
    public const string Field_Parameters = "Parameters";
    public const string Field_ValueEnums = "ValueEnums";
    public const string Field_PayloadFormat = "PayloadFormat";
    public const string Field_SkipGuardrails = "SkipGuardrails";
    public const string Field_PluginType = "PluginType";

    // Parameter name constants
    public const string Param_View = "view";
    public const string Param_Unit = "unit";
    public const string Param_Aggregation = "aggregation";

    // Data source type constants
    public const string SourceType_Insights = "insights";
    public const string SourceType_Cosmos = "cosmos";
    public const string SourceType_Kusto = "kusto";

    // Cosmos data source config fields
    public const string Field_Cosmos = "Cosmos";
    public const string Field_ConnectionKey = "ConnectionKey";
    public const string Field_Query = "Query";
    public const string Field_MaxItemCount = "MaxItemCount";
    public const string Field_SourceType = "SourceType";

    // Kusto data source config fields
    public const string Field_BaseQuery = "BaseQuery";
    public const string Field_DefaultTopN = "DefaultTopN";
    public const string Field_KustoExpression = "KustoExpression";

    // Post-processing config fields
    public const string Field_PostProcessingSchemaMapping = "PostProcessingSchemaMapping";
    public const string Field_SelectFields = "SelectFields";
}
