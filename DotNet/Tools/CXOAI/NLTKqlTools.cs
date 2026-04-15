using Azure.AI.OpenAI;
using Azure.Identity;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Provider.Interfaces;

namespace CXOAI.Tools;


public class NLTKqlTools : ToolBase
{
    private readonly ILogger<NLTKqlTools> _logger;

    private readonly ITreeConfigurationStoreProvider storeProvide;

    private readonly IUserAuthContext _authContext;

    private readonly IKustoProvider _kustoProvider;

    // Static schema graph data loaded once
    private static SchemaGraphData SchemaGraph = null!;
    private static string CompactEntityIndex = null!;
    private static Dictionary<string, List<string>> EntityFieldLookup = null!;
    private static Dictionary<string, List<SchemaRelationship>> RelationshipLookup = null!;

    private static void InitializeSchemaGraph()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "NLTKqlSchemaGraph.json");
        var json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        SchemaGraph = JsonSerializer.Deserialize<SchemaGraphData>(json, options) ?? new SchemaGraphData();

        // Build entity lookup: EntityName -> [FieldNames]
        EntityFieldLookup = SchemaGraph.Entities.ToDictionary(
            e => e.Name,
            e => e.Fields,
            StringComparer.OrdinalIgnoreCase);

        // Build relationship lookup: FromEntity -> [Relationships]
        RelationshipLookup = SchemaGraph.Relationships
            .GroupBy(r => r.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Build compact entity index: Name|Tags|Description (first 100 chars)
        var entityBuilder = new StringBuilder();
        foreach (var entity in SchemaGraph.Entities)
        {
            var desc = entity.Description.Length > 100
                ? entity.Description[..100] + "..."
                : entity.Description;
            var tags = entity.Tags.Count > 0 ? string.Join(",", entity.Tags) : "";
            entityBuilder.AppendLine($"{entity.Name}|{tags}|{desc}");
        }
        CompactEntityIndex = entityBuilder.ToString();
    }

    public NLTKqlTools(ILogger<NLTKqlTools> logger, ITreeConfigurationStoreProvider storeProvide, IUserAuthContext authContext, IToolStatusNotifier notifier, IKustoProvider kustoProvider) : base(notifier)
    {
        _logger = logger;
        this.storeProvide = storeProvide;
        _authContext = authContext;
        _kustoProvider = kustoProvider;
        if (SchemaGraph is null)
            InitializeSchemaGraph();
    }

    #region Query Normalization

    /// <summary>
    /// Normalizes KQL queries to fix common LLM-generated syntax issues.
    /// </summary>
    private static string NormalizeKqlQuery(string query)
    {
        // Fix: let IcmId = '123'; ... dynamic([IcmId]) → let IcmIds = dynamic(['123']);
        var dynamicWrapperMatch = System.Text.RegularExpressions.Regex.Match(
            query,
            @"let\s+(\w+)\s*=\s*'([^']+)';\s*(.*?)dynamic\s*\(\s*\[\s*\1\s*\]\s*\)",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (dynamicWrapperMatch.Success)
        {
            var varName = dynamicWrapperMatch.Groups[1].Value;
            var value = dynamicWrapperMatch.Groups[2].Value;
            var middle = dynamicWrapperMatch.Groups[3].Value;
            // Replace with correct pattern: let IcmIds = dynamic(['value']);
            var newVarName = varName.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? varName : varName + "s";
            query = query.Replace(
                dynamicWrapperMatch.Value,
                $"let {newVarName} = dynamic(['{value}']);\n{middle}{newVarName}");
        }

        // Fix: mv-expand todynamic(Customers) Customers → mv-expand todynamic(Customers)
        query = System.Text.RegularExpressions.Regex.Replace(
            query,
            @"mv-expand\s+todynamic\((\w+)\)\s+\1\b",
            "mv-expand todynamic($1)");

        // Fix: distinct <cols> | join ... on <joinCol> - ensure joinCol is in distinct
        // Pattern: | distinct X | join ... on Y  where Y is not in X
        var distinctJoinMatch = System.Text.RegularExpressions.Regex.Match(
            query,
            @"\|\s*distinct\s+([^|]+?)\s*\|\s*join\s+\w+\s*\([^)]+\)\s+on\s+(\w+)",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (distinctJoinMatch.Success)
        {
            var distinctCols = distinctJoinMatch.Groups[1].Value.Trim();
            var joinCol = distinctJoinMatch.Groups[2].Value.Trim();
            
            // Check if join column is missing from distinct columns
            if (!distinctCols.Contains(joinCol, StringComparison.OrdinalIgnoreCase))
            {
                // Add the join column to distinct
                var newDistinct = $"| distinct {distinctCols}, {joinCol}";
                var oldDistinct = $"| distinct {distinctCols}";
                query = query.Replace(oldDistinct, newDistinct);
            }
        }

        return query;
    }

    #endregion

    #region Core KQL Tools

    [Description("Creates a KqlRequest from KQL query and connection info. Use Cluster and Database from GetSchemaKnowledgeAsync result.")]
    public async Task<KqlRequest> GetKqlQuery(
        [Description("The KQL query string to execute.")] string kqlQuery,
        [Description("The cluster URL from GetSchemaKnowledgeAsync result.")] string cluster,
        [Description("The database name from GetSchemaKnowledgeAsync result.")] string database)
    {
        try
        {
            await NotifyAsync("🔍 Creating KQL request...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: GetKqlQuery | Cluster: {Cluster} | Database: {Database}", cluster, database);

            // Normalize query to fix common LLM-generated syntax issues
            var normalizedQuery = NormalizeKqlQuery(kqlQuery);

            var result = new KqlRequest
            {
                ClusterUrl = cluster,
                Database = database,
                Query = normalizedQuery
            };
            await NotifyAsync("✅ KQL request created");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: GetKqlQuery |  Query :{Query}", normalizedQuery);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKqlQuery | Cluster: {Cluster} | Database: {Database} | Query :{Query}", cluster, database, kqlQuery);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to generate Kql query");
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Executes a KQL query against an Azure Data Explorer cluster and returns the results. You MUST call GetKqlQuery first to get the KqlRequest.")]
    public async Task<string> ExecuteQueryAsync(
        [Description("The KqlRequest object returned by GetKqlQuery containing cluster URL, database, and query. Pass it unchanged.")] KqlRequest input)
    {
        try
        {
            await NotifyAsync("🔍 Executing KQL query...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: ExecuteQueryAsync | Cluster: {ClusterUrl} | Database: {Database}",
                input.ClusterUrl, input.Database);

            var dataTable = await _kustoProvider.ExecuteQueryAsync(input.Query);

            _logger.LogInformation("KQL query executed successfully");
            var response = ConvertDataTableToJson(dataTable);
            await NotifyAsync("✅ Query executed");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteQueryAsync | Cluster: {ClusterUrl} | Database: {Database}", input.ClusterUrl, input.Database);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to fetch data for Kql query");
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Creates multiple labeled KqlRequest objects for multi-part questions. Use when generating separate queries for different parts of a question.")]
    public async Task<List<LabeledKqlRequest>> CreateMultipleKqlQueries(
        [Description("Array of query definitions with label, kqlQuery, cluster, and database for each query.")] List<KqlQueryDefinition> queries)
    {
        try
        {
            await NotifyAsync("🔍 Creating multiple KQL requests...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: CreateMultipleKqlQueries | QueryCount: {Count}", queries.Count);

            var result = queries.Select(q => new LabeledKqlRequest
            {
                Label = q.Label,
                Request = new KqlRequest
                {
                    ClusterUrl = q.Cluster,
                    Database = q.Database,
                    Query = NormalizeKqlQuery(q.KqlQuery)
                }
            }).ToList();
            await NotifyAsync("✅ KQL requests created");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateMultipleKqlQueries | QueryCount: {Count}", queries.Count);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to create multiple Kql query");

            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Executes multiple KQL queries in parallel and returns labeled results. Use for multi-part questions requiring separate queries.")]
    public async Task<MultiQueryResult> ExecuteMultipleQueriesAsync(
        [Description("Array of labeled KQL requests to execute in parallel.")] List<LabeledKqlRequest> requests)
    {
        try
        {
            await NotifyAsync("🔍 Executing multiple KQL queries...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: ExecuteMultipleQueriesAsync | QueryCount: {Count}", requests.Count);

            var results = new MultiQueryResult { Results = [] };

            // Execute queries in parallel
            var tasks = requests.Select(async req =>
            {
                try
                {
                    var dataTable = await _kustoProvider.ExecuteQueryAsync(req.Request.Query);
                    return new QueryResult
                    {
                        Label = req.Label,
                        Success = true,
                        Data = ConvertDataTableToJson(dataTable),
                        Error = null
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Query '{Label}' failed", req.Label);
                    await NotifyAsync("❌ unable to get data for multiple Kql query");

                    return new QueryResult
                    {
                        Label = req.Label,
                        Success = false,
                        Data = null,
                        Error = ex.Message
                    };
                }
            });

            results.Results = (await Task.WhenAll(tasks)).ToList();

            _logger.LogInformation("Executed {Success}/{Total} queries successfully",
                results.Results.Count(r => r.Success), results.Results.Count);

            await NotifyAsync("✅ Queries executed");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteMultipleQueriesAsync | QueryCount: {Count}", requests.Count);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to get data for multiple Kql query");

            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Formats query results as a readable markdown table. Use after ExecuteQueryAsync for single query results.")]
    public async Task<string> FormatResultsAsMarkdown(
        [Description("JSON array string from ExecuteQueryAsync.")] string jsonData,
        [Description("Optional title for the results section.")] string? title = null,
        [Description("Maximum rows to display. Use -1 for all rows. Default is 50.")] int maxRows = 50)
    {
        try
        {
            await NotifyAsync("🔍 Formatting results...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: FormatResultsAsMarkdown");

            var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(jsonData);
            var result = FormatRowsAsMarkdown(rows, title, maxRows);
            await NotifyAsync("✅ Results formatted");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FormatResultsAsMarkdown");
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null!,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to format data for UI");
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Formats multiple query results as markdown tables with labels. Use after ExecuteMultipleQueriesAsync.")]
    public async Task<string> FormatMultipleResultsAsMarkdown(
        [Description("The MultiQueryResult object from ExecuteMultipleQueriesAsync.")] MultiQueryResult results,
        [Description("Maximum rows per query to display. Use -1 for all rows. Default is 25.")] int maxRowsPerQuery = 25)
    {
        try
        {
            await NotifyAsync("🔍 Formatting results...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: FormatMultipleResultsAsMarkdown | ResultCount: {Count}", results.Results.Count);

            var sb = new StringBuilder();

            foreach (var result in results.Results)
            {
                sb.AppendLine($"### {result.Label}");

                if (!result.Success)
                {
                    sb.AppendLine($"_Error: {result.Error}_\n");
                    continue;
                }

                if (string.IsNullOrEmpty(result.Data))
                {
                    sb.AppendLine("_No results_\n");
                    continue;
                }

                try
                {
                    var rows = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(result.Data);
                    sb.AppendLine(FormatRowsAsMarkdown(rows, null, maxRowsPerQuery));
                }
                catch (System.Text.Json.JsonException)
                {
                    sb.AppendLine("_Error parsing results_\n");
                }
                sb.AppendLine();
            }

            await NotifyAsync("✅ Results formatted");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FormatMultipleResultsAsMarkdown | ResultCount: {Count}", results.Results.Count);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null!,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            await NotifyAsync("❌ unable to format data for UI (multiple queries)");

            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Wraps the final formatted result in a CXOAgentResponse. Call this as the LAST step to return the skill's response.")]
    public async Task<CXOAgentResponse> ReturnFinalResponseAsync(
        [Description("The formatted markdown result string to return (from FormatResultsAsMarkdown or FormatMultipleResultsAsMarkdown).")] string formattedResult)
    {
        try
        {
            await NotifyAsync("✅ Returning final response");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: ReturnFinalResponseAsync");

            return new CXOAgentResponse
            {
                IsSuccess = true,
                Response = formattedResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReturnFinalResponseAsync");
            await NotifyAsync("❌ unable to respond to user request");
            return new CXOAgentResponse
            {
                IsSuccess = false,
                Response = $"Error returning response: {ex.Message}"
            };
        }
    }

    private static string FormatRowsAsMarkdown(List<Dictionary<string, object?>>? rows, string? title, int maxRows)
    {
        if (rows == null || rows.Count == 0)
            return title != null ? $"### {title}\n_No results_" : "_No results_";

        var sb = new StringBuilder();
        if (title != null) sb.AppendLine($"### {title}\n");

        // Get columns from first row
        var columns = rows[0].Keys.ToList();
        var rowCount = rows.Count;

        // Header row
        sb.AppendLine("| " + string.Join(" | ", columns.Select(TruncateCell)) + " |");
        sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

        // Data rows
        var displayRows = maxRows > 0 ? rows.Take(maxRows) : rows;
        foreach (var row in displayRows)
        {
            var values = columns.Select(c =>
            {
                var val = row.GetValueOrDefault(c);
                return TruncateCell(val?.ToString() ?? "");
            });
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        if (maxRows > 0 && rowCount > maxRows)
            sb.AppendLine($"\n_... and {rowCount - maxRows} more rows_");

        return sb.ToString();
    }

    private static string TruncateCell(string value, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (maxLength <= 3) return value; // Can't truncate with ellipsis if too short
        // Escape pipe characters for markdown table
        value = value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    [Description("Loads the schema graph test data (entities, relationships, and table mappings) from the SchemaGraphTestData.json file.")]
    public async Task<SchemaGraphData> LoadSchemaGraphAsync(
        [Description("Absolute or relative path to the SchemaGraphTestData.json file.")] string filePath)
    {
        try
        {
            await NotifyAsync("🔍 Loading schema graph...");
            _logger.LogInformation("Loading schema graph from {FilePath}", filePath);

            using var stream = File.OpenRead(filePath);
            var data = await JsonSerializer.DeserializeAsync<SchemaGraphData>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation(
                "Loaded schema graph: {EntityCount} entities, {RelationshipCount} relationships, {TableCount} tables",
                data?.Entities.Count ?? 0,
                data?.Relationships.Count ?? 0,
                data?.Tables.Count ?? 0);

            await NotifyAsync("✅ Schema graph loaded");
            return data ?? new SchemaGraphData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoadSchemaGraphAsync | FilePath: {FilePath}", filePath);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    #endregion

    #region Schema Graph Tools

    [Description("Gets complete schema knowledge for KQL generation. Returns JSON with Cluster, Database, and SchemaKnowledge for building KQL queries.")]
    public async Task<SchemaKnowledgeResult> GetSchemaKnowledgeAsync(
        [Description("The natural language query to extract schema context from.")] string query)
    {
        try
        {
            await NotifyAsync("🔍 Analyzing query and matching entities...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: GetSchemaKnowledgeAsync | Query: {Query}", query);

            var matchedEntities = await MatchEntitiesAsync(query);
            _logger.LogInformation("Matched entities: {Entities}", string.Join(", ", matchedEntities));

            var relevantTables = GetRelevantTables(matchedEntities);
            var targetTable = relevantTables.FirstOrDefault();

            await NotifyAsync("✅ Schema knowledge retrieved");
            return new SchemaKnowledgeResult
            {
                Cluster = targetTable?.Cluster,
                Database = targetTable?.Database,
                SchemaKnowledge = BuildSchemaKnowledge(matchedEntities, query)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetSchemaKnowledgeAsync | Query: {Query}", query);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    [Description("Analyzes a query using schema knowledge to decompose it into separate query intents. Call this AFTER GetSchemaKnowledgeAsync to determine if multiple queries are needed.")]
    public async Task<QueryPlan> DecomposeQueryAsync(
        [Description("The natural language query to analyze.")] string query,
        [Description("The schema knowledge from GetSchemaKnowledgeAsync.")] string schemaKnowledge)
    {
        try
        {
            await NotifyAsync("🔍 Decomposing query into intents...");
            _logger.LogInformation("Executing | Tool: NLTKqlTool | ToolName: DecomposeQueryAsync | Query: {Query}", query);

            var matchedEntities = await MatchEntitiesAsync(query);

            // Check for entity groups that require separate queries
            var entityGroups = AnalyzeEntityGroups(matchedEntities);

            // If only one logical group, single query is sufficient
            if (entityGroups.Count <= 1)
            {
                await NotifyAsync("✅ Query plan created");
                return new QueryPlan
                {
                    IsSingleQuery = true,
                    Intents = [new QueryIntent
                    {
                        Label = "Query Result",
                        Description = query,
                        TargetEntities = matchedEntities,
                        SuggestedTable = GetRelevantTables(matchedEntities).FirstOrDefault()?.Table
                    }]
                };
            }

            // Use LLM to decompose into semantic intents
            var intents = await DecomposeWithLlmAsync(query, matchedEntities);

            await NotifyAsync("✅ Query plan created");
            return new QueryPlan
            {
                IsSingleQuery = intents.Count <= 1,
                Intents = intents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DecomposeQueryAsync | Query: {Query}", query);
            var response = new CXOAgentResponse
            {
                IsSuccess = false,
                IsUIComponent = false,
                NeedsInputForUser = false,
                Payload = null,
                UIComponent = null,
                Response = $"{ex.Message},{ex.StackTrace}"
            };
            throw new ToolParameterException(JsonSerializer.Serialize(response));
        }
    }

    private List<List<string>> AnalyzeEntityGroups(List<string> entities)
    {
        // Group entities by their query purpose based on relationships
        var groups = new List<List<string>>();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            if (processed.Contains(entity)) continue;

            var group = new List<string> { entity };
            processed.Add(entity);

            // Find related entities that should be in same query (via direct relationships)
            if (RelationshipLookup.TryGetValue(entity, out var relationships))
            {
                foreach (var rel in relationships)
                {
                    // Reference tables (N:1) go in same query for enrichment
                    if (rel.Type == "N:1" && entities.Contains(rel.To) && !processed.Contains(rel.To))
                    {
                        group.Add(rel.To);
                        processed.Add(rel.To);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private async Task<List<QueryIntent>> DecomposeWithLlmAsync(string query, List<string> matchedEntities)
    {
        var exampleJson = """
            [
              {"Label": "Impacted S500 Customers", "Description": "List impacted S500 customers for ICM", "TargetEntities": ["Customer", "Incident"], "SuggestedTable": "ObserveCustomerModel"}
            ]
            """;

        // Use compact schema - only entity names, relationships, and table names
        // Decomposition only needs to understand entity relationships to split queries
        var compactSchema = BuildCompactSchemaForDecomposition(matchedEntities);

        var systemPrompt = $"""
            # Query Decomposition Agent
            
            You analyze user questions and break them into separate query intents.
            
            ## Matched Entities
            {string.Join(", ", matchedEntities)}
            
            ## Schema Summary
            {compactSchema}
            
            ## Rules
            1. ONLY generate intents for what the user EXPLICITLY asks for
            2. DO NOT add related queries the user didn't request
            3. "which/what/list" questions = LIST intent (return data rows)
            4. "how many/count/total" questions = COUNT intent (return aggregation)
            5. DO NOT auto-generate COUNT intents unless user explicitly asks for counts/totals
            6. Entities like Incident/ICM used as filters do NOT need their own intent
            7. If user asks ONE question, return exactly ONE intent
            8. "related incident" or "linked incident" to an ICM = SupportTicket entity (NOT Recommendation)
            
            ## Output Format (JSON array)
            {exampleJson}
            
            Return ONLY the JSON array, no markdown.
            """;

        string endpoint = SecretManager.GetAzureOpenAIRoleBaseAccessControl();
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        ChatClientAgent agent = client
            .GetChatClient("gpt-4o-mini")
            .AsAIAgent(instructions: systemPrompt);

        var session = await agent.CreateSessionAsync();
        _logger.LogInformation("Calling NLTKql.DecomposeQuery with query: {Query}", query);
        var response = await agent.RunAsync<List<QueryIntent>>(query, session);

        try
        {
            var result = response.Result;
            _logger.LogInformation("Called NLTKql.DecomposeQuery, here is response: {Count} intent(s): [{Intents}]",
                result?.Count ?? 0, string.Join(", ", (result ?? []).Select(i => $"{i.Label}({string.Join(",", i.TargetEntities)})")));
            return result ?? [new QueryIntent { Label = "Query Result", Description = query, TargetEntities = matchedEntities }];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM intent decomposition, falling back to single intent");
            return [new QueryIntent { Label = "Query Result", Description = query, TargetEntities = matchedEntities }];
        }
    }

    #endregion

    #region Entity Matching

    private List<string> MatchEntitiesByText(string query)
    {
        query = NormalizeQuery(query);
        var matchedEntities = new List<string>();

        var searchTerms = new List<(string Term, string EntityName, int Priority)>();

        foreach (var entity in SchemaGraph.Entities)
        {
            // Entity name - highest priority
            searchTerms.Add((entity.Name, entity.Name, 4));

            // Tags - high priority (same as name)
            foreach (var tag in entity.Tags)
            {
                searchTerms.Add((tag, entity.Name, 3));
            }

            // Fields - medium priority
            foreach (var field in entity.Fields)
            {
                searchTerms.Add((field, entity.Name, 2));
            }

            // Description keywords - lowest priority
            var keywords = ExtractKeywords(entity.Description);
            foreach (var keyword in keywords)
            {
                searchTerms.Add((keyword, entity.Name, 1));
            }
        }

        searchTerms = searchTerms
            .DistinctBy(t => (t.Term.ToLowerInvariant(), t.EntityName.ToLowerInvariant()))
            .OrderByDescending(t => t.Term.Split(' ').Length)
            .ThenByDescending(t => t.Term.Length)
            .ThenByDescending(t => t.Priority)
            .ToList();

        var remainingQuery = query;
        foreach (var (term, entityName, _) in searchTerms)
        {
            if (remainingQuery.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                if (!matchedEntities.Contains(entityName, StringComparer.OrdinalIgnoreCase))
                    matchedEntities.Add(entityName);
                remainingQuery = remainingQuery.Replace(term, " ", StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        return matchedEntities;
    }

    private async Task<List<string>> MatchEntitiesByLlmAsync(string query)
    {
        var systemPrompt = $"""
            # Entity Matcher for KQL Schema
            You are matching user queries to schema entities for KQL generation.
            
            ## Available Entities (Name|Tags|Description):
            {CompactEntityIndex}
            
            ## Instructions:
            1. Identify which entities the user's query refers to
            2. Return ONLY entity names that exist in the index above
            3. Return as JSON string array: ["Entity1", "Entity2"]
            4. Return [] if no entities match
            5. Do NOT wrap in markdown code fences
            
            ## Examples:
            - "show incidents for customer X" → ["Incident", "Customer"]
            - "recommendations linked to ICM" → ["Recommendation", "Incident"]
            - "support tickets with critical severity" → ["SupportTicket"]
            """;

        string endpoint = SecretManager.GetAzureOpenAIRoleBaseAccessControl();
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        ChatClientAgent agent = client
            .GetChatClient("gpt-4o-mini")
            .AsAIAgent(instructions: systemPrompt);

        var session = await agent.CreateSessionAsync();
        _logger.LogInformation("Calling NLTKql.MatchEntitiesByLlm with query: {Query}", query);
        var response = await agent.RunAsync<List<string>>(query, session);

        List<string> llmMatches;
        try
        {
            llmMatches = response.Result ?? [];
            _logger.LogInformation("Called NLTKql.MatchEntitiesByLlm, here is response: [{Entities}]",
                string.Join(", ", llmMatches));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var text = response.Text ?? string.Empty;
            llmMatches = ParseJsonArrayFromText(text);
        }

        return llmMatches
            .Where(name => EntityFieldLookup.ContainsKey(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> MatchEntitiesAsync(string query)
    {
        var textMatches = MatchEntitiesByText(query);
        if (textMatches.Count > 0)
            return textMatches;
        return await MatchEntitiesByLlmAsync(query);
    }

    #endregion

    #region Knowledge Building

    /// <summary>
    /// Builds a compact schema summary for query decomposition (entity names, relationships, table names only).
    /// This is much smaller than the full schema knowledge to avoid token limits.
    /// The decomposition step only needs to understand WHAT entities exist and their relationships
    /// to decide if multiple queries are needed - it doesn't need column mappings, filters, etc.
    /// </summary>
    private string BuildCompactSchemaForDecomposition(List<string> entityNames)
    {
        var builder = new StringBuilder();

        // Entity list with relationships only (no fields, no column mappings)
        foreach (var entityName in entityNames)
        {
            var entity = SchemaGraph.Entities.FirstOrDefault(e =>
                e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
            if (entity is null) continue;

            builder.AppendLine($"- {entity.Name}: {entity.Description}");
            if (RelationshipLookup.TryGetValue(entity.Name, out var relationships))
            {
                foreach (var rel in relationships)
                {
                    builder.AppendLine($"  → {rel.To} ({rel.Type})");
                }
            }
        }

        // Relevant table names only (no column details)
        var tables = GetRelevantTables(entityNames);
        if (tables.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Tables: " + string.Join(", ", tables.Select(t => t.Table)));
        }

        return builder.ToString();
    }

    private string BuildSchemaKnowledge(List<string> entityNames, string originalQuery)
    {
        var builder = new StringBuilder();
        var described = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryLower = originalQuery.ToLowerInvariant();

        builder.AppendLine("# Schema Knowledge for KQL Generation");
        builder.AppendLine();

        // Add KQL best practices FIRST - so they're prioritized
        builder.AppendLine("## ⚠️ CRITICAL: KQL Best Practices (MUST FOLLOW)");
        builder.AppendLine();
        builder.AppendLine("### 1. Join Filter Placement - MANDATORY");
        builder.AppendLine("- **NEVER** apply filters AFTER a join");
        builder.AppendLine("- **ALWAYS** wrap the right-side table in parentheses and apply filters INSIDE");
        builder.AppendLine("- Pattern: `| join kind=inner (RightTable | where Filter) on JoinKey`");
        builder.AppendLine();
        builder.AppendLine("### 2. Pre-Filter Left Side");
        builder.AppendLine("- Apply any filters on the left-side data BEFORE the join to reduce data volume");
        builder.AppendLine();
        builder.AppendLine("### 3. Query Order");
        builder.AppendLine("1. Base table/function call");
        builder.AppendLine("2. Transformations (mv-expand, extend)");
        builder.AppendLine("3. Left-side filters");
        builder.AppendLine("4. Join with right-side filters INSIDE parentheses");
        builder.AppendLine("5. Aggregation or projection");
        builder.AppendLine();

        builder.AppendLine("## Entities");
        foreach (var entityName in entityNames)
        {
            var entity = SchemaGraph.Entities.FirstOrDefault(e =>
                e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
            if (entity is null || !described.Add(entity.Name))
                continue;

            builder.AppendLine();
            builder.AppendLine($"### {entity.Name}");
            builder.AppendLine($"**Description:** {entity.Description}");
            if (entity.Tags.Count > 0)
            {
                builder.AppendLine($"**Also known as:** {string.Join(", ", entity.Tags)}");
            }
            builder.AppendLine($"**Fields:** {string.Join(", ", entity.Fields)}");

            if (RelationshipLookup.TryGetValue(entity.Name, out var relationships))
            {
                builder.AppendLine($"**Relationships:**");
                foreach (var rel in relationships)
                {
                    builder.AppendLine($"  - → {rel.To} ({rel.Type}) via {FormatJoinKeys(rel.JoinKeys)}");
                    if (!string.IsNullOrEmpty(rel.DataCompleteness) && rel.DataCompleteness != "complete")
                    {
                        builder.AppendLine($"    ⚠️ Data: {rel.DataCompleteness}");
                    }
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Physical Tables");
        var relevantTables = GetRelevantTables(entityNames);
        foreach (var table in relevantTables)
        {
            builder.AppendLine();
            builder.AppendLine($"### {table.Table}");
            builder.AppendLine($"**Cluster:** {table.Cluster}");
            builder.AppendLine($"**Database:** {table.Database}");
            if (table.CrossCluster)
            {
                try
                {
                    builder.AppendLine($"**⚠️ CROSS-CLUSTER TABLE - Use this syntax:** `{table.GetCrossClusterAnnotation()}`");
                }
                catch (UriFormatException ex)
                {
                    _logger.LogWarning(ex, "Invalid cluster URI for table {Table}", table.Table);
                    builder.AppendLine($"**⚠️ CROSS-CLUSTER TABLE - Invalid cluster URI**");
                }
            }
            builder.AppendLine($"**Description:** {table.Description}");

            if (table.Deduplication is not null)
            {
                var groupBy = string.Join(", ", table.Deduplication.GroupByColumns);
                builder.AppendLine($"**Deduplication (APPLY IMMEDIATELY AFTER TABLE, BEFORE WHERE):** `| summarize {table.Deduplication.Function}({table.Deduplication.OrderByColumn}, *) by {groupBy}`");
            }

            builder.AppendLine("**Column Mappings:**");
            foreach (var (entityField, mapping) in table.FieldMapping)
            {
                var entName = entityField.Split('.')[0];
                if (entityNames.Any(e => e.Equals(entName, StringComparison.OrdinalIgnoreCase)))
                {
                    builder.AppendLine($"  - {entityField} → `{mapping.TableColumnName}` ({mapping.Type})");
                }
            }

            if (table.ImplicitFilters.Count > 0)
            {
                builder.AppendLine("**Implicit Filters (COPY EXACTLY):**");
                foreach (var filter in table.ImplicitFilters)
                {
                    // Get the physical column name 
                    var physicalColumn = filter.Field.Split('.').Last();
                    // Try to find mapping for this field
                    var mapping = table.FieldMapping.FirstOrDefault(kv => kv.Key.Equals(filter.Field, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(mapping.Value?.TableColumnName))
                        physicalColumn = mapping.Value.TableColumnName;

                    // Format value - quote strings, leave booleans unquoted
                    var formattedValue = filter.Value.ToLowerInvariant() switch
                    {
                        "true" => "true",
                        "false" => "false",
                        _ => $"\"{filter.Value}\""
                    };
                    builder.AppendLine($"  - `| where {physicalColumn} {filter.Operator} {formattedValue}`");
                }
            }
        }

        var relevantFunctions = GetRelevantFunctions(entityNames);
        if (relevantFunctions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Available Functions");
            foreach (var func in relevantFunctions)
            {
                builder.AppendLine();
                builder.AppendLine($"### {func.Name}");
                builder.AppendLine($"**Database:** {func.Database}");
                builder.AppendLine($"**Description:** {func.Description}");
                if (func.TriggerTags.Count > 0)
                {
                    builder.AppendLine($"**Use when query mentions:** {string.Join(", ", func.TriggerTags)}");
                }
                builder.AppendLine($"**Parameters:**");
                foreach (var param in func.Parameters)
                {
                    var defaultVal = param.DefaultValue != null ? $" = {param.DefaultValue}" : "";
                    builder.AppendLine($"  - `{param.Name}`: {param.Type}{defaultVal}");
                }
                if (func.Transformations.Count > 0)
                {
                    builder.AppendLine($"**Required Transformations (APPLY CONDITIONALLY):**");
                    foreach (var transform in func.Transformations)
                    {
                        builder.AppendLine($"  - `| {transform.Operation} {transform.Column}` - {transform.Reason}");
                        if (!string.IsNullOrEmpty(transform.WhenRequired))
                        {
                            builder.AppendLine($"    - **When to apply:** {transform.WhenRequired}");
                        }
                        if (transform.ExtendColumns.Count > 0)
                        {
                            var extends = string.Join(", ", transform.ExtendColumns.Select(kv => $"{kv.Key} = {kv.Value}"));
                            builder.AppendLine($"  - `| extend {extends}`");
                        }
                    }
                }
                if (func.UsagePatterns.Count > 0)
                {
                    builder.AppendLine($"**Usage Patterns (CHOOSE THE APPROPRIATE ONE):**");
                    foreach (var pattern in func.UsagePatterns)
                    {
                        builder.AppendLine($"*{pattern.Key}:*");
                        builder.AppendLine($"```kql");
                        builder.AppendLine(pattern.Value);
                        builder.AppendLine($"```");
                    }
                }
                else if (!string.IsNullOrEmpty(func.UsagePattern))
                {
                    builder.AppendLine($"**Usage Pattern (COPY THIS):**");
                    builder.AppendLine($"```kql");
                    builder.AppendLine(func.UsagePattern);
                    builder.AppendLine($"```");
                }
                // Group output columns by entity for clarity
                var columnsByEntity = func.OutputMapping
                    .GroupBy(kv => kv.Key.Split('.')[0])
                    .ToDictionary(g => g.Key, g => g.Select(kv => kv.Value.TableColumnName).ToList());
                builder.AppendLine($"**Output Columns (select based on target entity):**");
                foreach (var entity in columnsByEntity)
                {
                    builder.AppendLine($"  - {entity.Key}: {string.Join(", ", entity.Value)}");
                }
            }
        }

        // Add join keys section - only for lookup/reference tables, not filter columns
        if (relevantTables.Count > 1)
        {
            // Find columns that appear in multiple tables (potential join keys)
            var columnToTables = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in relevantTables)
            {
                foreach (var mapping in table.FieldMapping.Values)
                {
                    if (!columnToTables.ContainsKey(mapping.TableColumnName))
                        columnToTables[mapping.TableColumnName] = new List<string>();
                    if (!columnToTables[mapping.TableColumnName].Contains(table.Table))
                        columnToTables[mapping.TableColumnName].Add(table.Table);
                }
            }

            // Only suggest joins for TypeId/lookup columns, not for IncidentId (which is for filtering)
            var joinKeys = columnToTables
                .Where(kv => kv.Value.Count > 1)
                .Where(kv => kv.Key.EndsWith("TypeId", StringComparison.OrdinalIgnoreCase) ||
                             kv.Key.Contains("Type", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (joinKeys.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Join Keys (JOIN these tables on shared column)");
                foreach (var (column, tables) in joinKeys)
                {
                    builder.AppendLine($"- **{column}**: {string.Join(" ↔ ", tables)}");
                }
            }

            // Suggest useful project columns
            builder.AppendLine();
            builder.AppendLine("## Suggested Project Columns");
            var projectColumns = new List<string>();
            foreach (var table in relevantTables)
            {
                foreach (var (field, mapping) in table.FieldMapping)
                {
                    // Include descriptive columns and IDs
                    var col = mapping.TableColumnName;
                    if (col.Contains("Label") || col.Contains("Description") || col.Contains("Category") ||
                        col.Contains("Impact") || col.Contains("Benefit") || col.Contains("AcmId") ||
                        col.EndsWith("TypeId") || col.Contains("Status"))
                    {
                        if (!projectColumns.Contains(col))
                            projectColumns.Add(col);
                    }
                }
            }
            builder.AppendLine($"`| project {string.Join(", ", projectColumns)}`");
        }

        // Add query rules section - filter to only rules triggered by matched entities or query keywords
        var relevantRules = SchemaGraph.QueryRules.Where(rule =>
            rule.TriggeredBy.Count == 0 || // Rules with no triggers always apply
            rule.TriggeredBy.Any(trigger =>
                entityNames.Any(e => e.Equals(trigger, StringComparison.OrdinalIgnoreCase)) ||
                queryLower.Contains(trigger.ToLowerInvariant()))
        ).ToList();

        if (relevantRules.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Query Rules (FOLLOW THESE)");
            foreach (var rule in relevantRules)
            {
                builder.AppendLine();
                builder.AppendLine($"### {rule.Name}");
                builder.AppendLine($"**Rule:** {rule.Description}");
                if (rule.TriggeredBy.Count > 0)
                {
                    builder.AppendLine($"**Apply when query mentions:** {string.Join(", ", rule.TriggeredBy)}");
                }
                if (!string.IsNullOrEmpty(rule.OutputFormat))
                {
                    builder.AppendLine($"**Output:** {rule.OutputFormat}");
                }
                if (!string.IsNullOrEmpty(rule.Pattern))
                {
                    builder.AppendLine($"**Pattern:** `{rule.Pattern}`");
                }
                if (!string.IsNullOrEmpty(rule.Example))
                {
                    builder.AppendLine($"**Example:** `{rule.Example}`");
                }
                if (!string.IsNullOrEmpty(rule.JoinPattern))
                {
                    builder.AppendLine($"**Join Pattern:** `{rule.JoinPattern}`");
                }
                if (!string.IsNullOrEmpty(rule.Important))
                {
                    builder.AppendLine($"**IMPORTANT:** {rule.Important}");
                }

                // Compose query from Composition if present
                if (rule.Composition is not null)
                {
                    var composedQuery = ComposeQueryFromRule(rule.Composition);
                    if (!string.IsNullOrEmpty(composedQuery))
                    {
                        builder.AppendLine($"**Generated Pattern:**");
                        builder.AppendLine($"```kql");
                        builder.AppendLine(composedQuery);
                        builder.AppendLine($"```");
                    }
                }
            }
        }

        return builder.ToString();
    }

    private string ComposeQueryFromRule(QueryComposition composition)
    {
        var lines = new List<string>();

        // Get base table metadata
        SchemaTable? baseTable = null;
        if (!string.IsNullOrEmpty(composition.BaseTable))
        {
            baseTable = SchemaGraph.Tables.FirstOrDefault(t =>
                t.Table.Equals(composition.BaseTable, StringComparison.OrdinalIgnoreCase));

            if (baseTable != null)
            {
                lines.Add($"database('{baseTable.Database}').{baseTable.Table}");

                // Apply deduplication
                if (composition.ApplyDeduplication && baseTable.Deduplication is not null)
                {
                    var groupBy = string.Join(", ", baseTable.Deduplication.GroupByColumns);
                    lines.Add($"| summarize {baseTable.Deduplication.Function}({baseTable.Deduplication.OrderByColumn}, *) by {groupBy}");
                }

                // Apply implicit filters
                if (composition.ApplyImplicitFilters && baseTable.ImplicitFilters.Count > 0)
                {
                    var filters = baseTable.ImplicitFilters.Select(f =>
                    {
                        var col = f.Field.Split('.').Last();
                        var mapping = baseTable.FieldMapping.FirstOrDefault(m => m.Key.Equals(f.Field, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(mapping.Value?.TableColumnName))
                            col = mapping.Value.TableColumnName;
                        var val = f.Value.ToLowerInvariant() is "true" or "false" ? f.Value.ToLowerInvariant() : $"\"{f.Value}\"";
                        return $"{col} {f.Operator} {val}";
                    });
                    lines.Add($"| where {string.Join(" and ", filters)}");
                }
            }
        }

        // Apply filter parameter
        if (composition.FilterParam is not null)
        {
            var val = composition.FilterParam.Placeholder ?? $"\"{composition.FilterParam.Value}\"";
            lines.Add($"| where {composition.FilterParam.Field} {composition.FilterParam.Operator} '{val}'");
        }

        // Apply pre-join filter (e.g., | where CustomerType == 'Enterprise')
        if (composition.PreJoinFilter is not null)
        {
            var val = FormatKqlValue(composition.PreJoinFilter.Value);
            lines.Add($"| where {composition.PreJoinFilter.Field} {composition.PreJoinFilter.Operator} {val}");
        }

        // Apply pre-join distinct on join key to reduce data volume before joining
        if (composition.PreJoinDistinct && composition.JoinOn is not null)
        {
            lines.Add($"| distinct {composition.JoinOn.Left}");
        }

        // Apply join
        if (!string.IsNullOrEmpty(composition.JoinTable))
        {
            var joinTable = SchemaGraph.Tables.FirstOrDefault(t =>
                t.Table.Equals(composition.JoinTable, StringComparison.OrdinalIgnoreCase));

            if (joinTable != null)
            {
                // Use cross-cluster syntax if table is on different cluster
                string tableRef;
                if (joinTable.CrossCluster)
                {
                    try
                    {
                        tableRef = joinTable.GetCrossClusterAnnotation();
                    }
                    catch (UriFormatException ex)
                    {
                        _logger.LogWarning(ex, "Invalid cluster URI for join table {Table}, falling back to database syntax", joinTable.Table);
                        tableRef = $"database('{joinTable.Database}').{joinTable.Table}";
                    }
                }
                else
                {
                    tableRef = $"database('{joinTable.Database}').{joinTable.Table}";
                }

                // Build join subquery with optional filter inside and project rename
                var subQueryParts = new List<string> { tableRef };

                if (composition.FilterInsideJoin is not null)
                {
                    var filterVal = FormatKqlValue(composition.FilterInsideJoin.Value);
                    subQueryParts.Add($"| where {composition.FilterInsideJoin.Field} {composition.FilterInsideJoin.Operator} {filterVal}");
                }

                // Determine join on clause — rename column in project if left/right names differ
                string joinOnClause;
                if (composition.JoinOn != null)
                {
                    if (!composition.JoinOn.Left.Equals(composition.JoinOn.Right, StringComparison.OrdinalIgnoreCase))
                    {
                        var projectColumns = new List<string> { $"{composition.JoinOn.Left} = {composition.JoinOn.Right}" };
                        if (composition.FilterInsideJoin is not null)
                            projectColumns.Add(composition.FilterInsideJoin.Field);
                        subQueryParts.Add($"| project {string.Join(", ", projectColumns)}");
                        joinOnClause = composition.JoinOn.Left;
                    }
                    else
                    {
                        joinOnClause = $"$left.{composition.JoinOn.Left} == $right.{composition.JoinOn.Right}";
                    }
                }
                else
                {
                    joinOnClause = FindJoinKey(baseTable, joinTable);
                }

                lines.Add($"| join kind=inner ({string.Join(" ", subQueryParts)}) on {joinOnClause}");
            }
        }

        // Apply post-join distinct
        if (composition.PostJoinDistinct.Count > 0)
        {
            lines.Add($"| distinct {string.Join(", ", composition.PostJoinDistinct)}");
        }

        // Apply additional filter
        if (composition.AdditionalFilter is not null)
        {
            var val = composition.AdditionalFilter.Value ?? composition.AdditionalFilter.Placeholder;
            var formattedVal = val?.ToLowerInvariant() is "true" or "false" or "yes" or "no"
                ? $"'{val}'"
                : $"\"{val}\"";
            lines.Add($"| where {composition.AdditionalFilter.Field} {composition.AdditionalFilter.Operator} {formattedVal}");
        }

        // Apply output columns
        if (composition.OutputColumns.Count > 0)
        {
            var cmd = composition.Distinct ? "distinct" : "project";
            lines.Add($"| {cmd} {string.Join(", ", composition.OutputColumns)}");
        }

        return string.Join("\n", lines);
    }

    private string FindJoinKey(SchemaTable? leftTable, SchemaTable rightTable)
    {
        if (leftTable == null) return "";

        // Find common columns between tables
        var leftColumns = leftTable.FieldMapping.Values.Select(m => m.TableColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightColumns = rightTable.FieldMapping.Values.Select(m => m.TableColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commonColumn = leftColumns.FirstOrDefault(c => rightColumns.Contains(c));
        return commonColumn != null ? $"$left.{commonColumn} == $right.{commonColumn}" : "";
    }

    private static string FormatKqlValue(string? value) =>
        value?.ToLowerInvariant() is "true" or "false"
            ? value.ToLowerInvariant()
            : $"'{value}'";

    #endregion

    #region Helpers

    private static string NormalizeQuery(string query) =>
        query.Replace(",", " ").Replace(";", " ").Replace("  ", " ").Trim();

    private static string ConvertDataTableToJson(DataTable dataTable)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in dataTable.Rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (DataColumn col in dataTable.Columns)
            {
                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            }
            rows.Add(dict);
        }
        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "of", "for", "to", "in", "on", "with", "is", "are", "was", "were"
        };

        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', ';', '(', ')'))
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatJoinKeys(Dictionary<string, string> joinKeys) =>
        string.Join(", ", joinKeys.Select(kv => $"{kv.Key} = {kv.Value}"));

    private List<SchemaTable> GetRelevantTables(List<string> entityNames)
    {
        // Expand entity list to include related entities (via relationships)
        var expandedEntities = new HashSet<string>(entityNames, StringComparer.OrdinalIgnoreCase);

        foreach (var entityName in entityNames)
        {
            // Add entities we can reach FROM this entity
            if (RelationshipLookup.TryGetValue(entityName, out var outgoingRels))
            {
                foreach (var rel in outgoingRels)
                    expandedEntities.Add(rel.To);
            }

            // Add entities that point TO this entity
            foreach (var rel in SchemaGraph.Relationships.Where(r =>
                r.To.Equals(entityName, StringComparison.OrdinalIgnoreCase)))
            {
                expandedEntities.Add(rel.From);
            }
        }

        // Find tables that have field mappings for any of the expanded entities
        return SchemaGraph.Tables.Where(table =>
            table.FieldMapping.Keys.Any(field =>
                expandedEntities.Any(entity =>
                    field.StartsWith(entity + ".", StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    private List<SchemaFunction> GetRelevantFunctions(List<string> entityNames) =>
        SchemaGraph.Functions.Where(func =>
            func.OutputMapping.Keys.Any(field =>
                entityNames.Any(entity =>
                    field.StartsWith(entity + ".", StringComparison.OrdinalIgnoreCase))))
            .ToList();

    private static List<string> ParseJsonArrayFromText(string text)
    {
        var cleaned = text.Replace("```json", "").Replace("```", "").Trim();
        var start = cleaned.IndexOf('[');
        var end = cleaned.LastIndexOf(']');
        if (start >= 0 && end > start)
        {
            var jsonFragment = cleaned[start..(end + 1)];
            try { return JsonSerializer.Deserialize<List<string>>(jsonFragment) ?? []; }
            catch { }
        }
        return [];
    }

    #endregion
}

[Description("Result from GetSchemaKnowledgeAsync containing target cluster/database and schema knowledge for KQL generation.")]
public class SchemaKnowledgeResult
{
    [Description("The target Azure Data Explorer cluster URL.")]
    public string Cluster { get; set; } = string.Empty;

    [Description("The target database name.")]
    public string Database { get; set; } = string.Empty;

    [Description("Schema knowledge markdown for KQL generation (entities, tables, columns, filters, joins).")]
    public string SchemaKnowledge { get; set; } = string.Empty;
}

[Description("Input for ExecuteQueryAsync containing the Kusto cluster URL, database name, and KQL query.")]
public class KqlRequest
{
    [Description("The Azure Data Explorer cluster URL to execute the query against.")]
    public string ClusterUrl { get; set; } = string.Empty;

    [Description("The database name within the cluster.")]
    public string Database { get; set; } = string.Empty;

    [Description("The KQL query string to execute.")]
    public string Query { get; set; } = string.Empty;
}

[Description("Input for CreateMultipleKqlQueries - defines a single query with its label and connection info.")]
public class KqlQueryDefinition
{
    [Description("A descriptive label for this query (e.g., 'S500 Customer Count', 'Support Tickets').")]
    public string Label { get; set; } = string.Empty;

    [Description("The KQL query string.")]
    public string KqlQuery { get; set; } = string.Empty;

    [Description("The cluster URL.")]
    public string Cluster { get; set; } = string.Empty;

    [Description("The database name.")]
    public string Database { get; set; } = string.Empty;
}

[Description("A labeled KQL request for batch execution.")]
public class LabeledKqlRequest
{
    [Description("The label identifying this query's purpose.")]
    public string Label { get; set; } = string.Empty;

    [Description("The KQL request to execute.")]
    public KqlRequest Request { get; set; } = new();
}

[Description("Result from ExecuteMultipleQueriesAsync containing all query results.")]
public class MultiQueryResult
{
    [Description("List of results from each query, labeled by purpose.")]
    public List<QueryResult> Results { get; set; } = [];
}

[Description("Result from a single query in a batch execution.")]
public class QueryResult
{
    [Description("The label identifying which query this result is from.")]
    public string Label { get; set; } = string.Empty;

    [Description("Whether the query executed successfully.")]
    public bool Success { get; set; }

    [Description("The query result data as JSON string (null if failed).")]
    public string? Data { get; set; }

    [Description("Error message if the query failed (null if successful).")]
    public string? Error { get; set; }
}

[Description("Result from DecomposeQueryAsync - a plan indicating whether single or multiple queries are needed.")]
public class QueryPlan
{
    [Description("True if a single query can answer the user's question, false if multiple queries are needed.")]
    public bool IsSingleQuery { get; set; }

    [Description("List of query intents detected from the user's question. Use these to build separate queries.")]
    public List<QueryIntent> Intents { get; set; } = [];
}

[Description("A single query intent extracted from a complex question.")]
public class QueryIntent
{
    [Description("A descriptive label for this intent (e.g., 'S500 Customer Count').")]
    public string Label { get; set; } = string.Empty;

    [Description("What this query should retrieve.")]
    public string Description { get; set; } = string.Empty;

    [Description("Entities involved in this query intent.")]
    public List<string> TargetEntities { get; set; } = [];

    [Description("Suggested table to query (if known from schema analysis).")]
    public string? SuggestedTable { get; set; }
}