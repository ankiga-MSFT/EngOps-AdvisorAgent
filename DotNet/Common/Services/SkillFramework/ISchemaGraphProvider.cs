namespace CXOAI.SkillFramework;

/// <summary>
/// Provides the full schema graph data: entities, relationships, and table mappings.
/// This is the central data source for entity validation, path resolution, and table mapping.
///
/// <para><b>Python equivalent:</b> <c>SchemaGraph</c> class loaded from <c>GraphConfig.json</c>,
/// providing <c>self.schema.graph</c> (NetworkX graph), <c>self.schema.tables</c>,
/// and <c>self.schema.relationships</c>.</para>
///
/// <para><b>POC:</b> Loads from <c>GraphConfig.json</c> on disk.</para>
/// <para><b>Production:</b> Queries Kusto <c>SchemaEntities</c>/<c>SchemaRelationships</c>/<c>SchemaTables</c>
/// tables for the active graph version (stored by IndexingPipeline via
/// <c>core.data.kusto_repository</c>), or Cosmos DB Gremlin graph
/// (synced by <c>core.data.cosmos_repository</c>).</para>
/// </summary>
public interface ISchemaGraphProvider
{
    /// <summary>
    /// Loads the complete schema graph (entities, relationships, tables).
    /// Called by Steps 2, 3, and 4 to access schema metadata.
    /// </summary>
    Task<SchemaGraphData> GetSchemaGraphAsync(CancellationToken cancellationToken = default);
}

/// <summary>Full schema graph: all entities, relationships, physical table definitions, and functions.</summary>
public class SchemaGraphData
{
    public List<SchemaEntity> Entities { get; set; } = [];
    public List<SchemaRelationship> Relationships { get; set; } = [];
    public List<SchemaTable> Tables { get; set; } = [];
    public List<SchemaFunction> Functions { get; set; } = [];
    public List<QueryRule> QueryRules { get; set; } = [];
}

/// <summary>A graph entity node with name, description, and field names.</summary>
public class SchemaEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// A relationship edge between two entities with cardinality, data completeness,
/// and join key metadata. Used for path resolution and table join planning.
/// </summary>
public class SchemaRelationship
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DataCompleteness { get; set; } = string.Empty;
    public string? CompletenessNote { get; set; }
    public SchemaPrerequisite? Prerequisite { get; set; }
    public List<string> Labels { get; set; } = [];
    public Dictionary<string, string> JoinKeys { get; set; } = [];
}

/// <summary>Prerequisite filter that must be met for conditional data completeness.</summary>
public class SchemaPrerequisite
{
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>Physical Kusto table with cluster, database, field mappings, and implicit filters.</summary>
public class SchemaTable
{
    public string Table { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CrossCluster { get; set; } = false;
    public DeduplicationRule? Deduplication { get; set; }
    public Dictionary<string, FieldMappingInfo> FieldMapping { get; set; } = [];
    public List<ImplicitFilterInfo> ImplicitFilters { get; set; } = [];

    /// <summary>Gets the cross-cluster annotation syntax from Cluster, Database, and Table.</summary>
    public string GetCrossClusterAnnotation()
    {
        if (Uri.TryCreate(Cluster, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            var scheme = uri.Scheme;
            var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
            return $"cluster('{scheme}://{host}{port}').database('{Database}').{Table}";
        }
        else
        {
            // Assume Cluster is a bare hostname
            return $"cluster('{Cluster}').database('{Database}').{Table}";
        }
    }
}

/// <summary>Deduplication rule to get latest/first record per group using arg_max/arg_min.</summary>
public class DeduplicationRule
{
    public string Function { get; set; } = "arg_max";
    public string OrderByColumn { get; set; } = string.Empty;
    public List<string> GroupByColumns { get; set; } = [];
}

/// <summary>Kusto function with cluster, database, parameters, and return schema.</summary>
public class SchemaFunction
{
    public string Name { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string? Folder { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> TriggerTags { get; set; } = [];
    public List<FunctionTransformation> Transformations { get; set; } = [];
    public string? UsagePattern { get; set; }
    public Dictionary<string, string> UsagePatterns { get; set; } = [];
    public List<FunctionParameter> Parameters { get; set; } = [];
    public Dictionary<string, FieldMappingInfo> OutputMapping { get; set; } = [];
}

/// <summary>A transformation required when using a function output (e.g., mv-expand for arrays).</summary>
public class FunctionTransformation
{
    public string Column { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? WhenRequired { get; set; }
    public Dictionary<string, string> ExtendColumns { get; set; } = [];
}

/// <summary>A parameter for a Kusto function with name, type, and optional default value.</summary>
public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}

/// <summary>Maps a graph-level field (Entity.Field) to a physical Kusto column name and type.</summary>
public class FieldMappingInfo
{
    public string TableColumnName { get; set; } = string.Empty;
    public string Type { get; set; } = "String";
}

/// <summary>An implicit filter automatically applied to table queries (e.g., IsActive == true).</summary>
public class ImplicitFilterInfo
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>A query generation rule that provides patterns for specific scenarios.</summary>
public class QueryRule
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> TriggeredBy { get; set; } = [];
    public string? OutputFormat { get; set; }
    public string? Pattern { get; set; }
    public string? Example { get; set; }
    public string? JoinPattern { get; set; }
    public string? Important { get; set; }
    public QueryComposition? Composition { get; set; }
}

/// <summary>Defines how to compose a query from schema elements.</summary>
public class QueryComposition
{
    public string? BaseTable { get; set; }
    public bool ApplyDeduplication { get; set; }
    public bool ApplyImplicitFilters { get; set; }
    public FilterParam? FilterParam { get; set; }
    /// <summary>Filter applied before the join (e.g., CustomerType == 'Enterprise').</summary>
    public FilterParam? PreJoinFilter { get; set; }
    /// <summary>When true, adds distinct on the join key column before joining to reduce data volume.</summary>
    public bool PreJoinDistinct { get; set; }
    public string? JoinTable { get; set; }
    public JoinOn? JoinOn { get; set; }
    /// <summary>Filter applied inside the join subquery (e.g., S500 == 'Yes').</summary>
    public FilterParam? FilterInsideJoin { get; set; }
    /// <summary>Columns for distinct after the join (e.g., ["CustomerName"]).</summary>
    public List<string> PostJoinDistinct { get; set; } = [];
    public FilterParam? AdditionalFilter { get; set; }
    public List<string> OutputColumns { get; set; } = [];
    public bool Distinct { get; set; }
}

/// <summary>A filter parameter with field, operator, and value or placeholder.</summary>
public class FilterParam
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "==";
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
}

/// <summary>Join condition specifying left and right columns.</summary>
public class JoinOn
{
    public string Left { get; set; } = string.Empty;
    public string Right { get; set; } = string.Empty;
}
