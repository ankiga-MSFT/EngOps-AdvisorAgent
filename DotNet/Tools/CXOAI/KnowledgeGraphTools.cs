using Azure.AI.OpenAI;
using Azure.Identity;
using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using Microsoft.Agents.AI;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CXOAI.Tools;

public class KnowledgeGraphTools
{
    public static Dictionary<Node, List<Relationship>> graph = new Dictionary<Node, List<Relationship>>();
    private static Dictionary<string, Dictionary<string, string>> nodeFilters = new(StringComparer.OrdinalIgnoreCase);
    private static string CompactNodeIndex;
    private readonly Dictionary<string, string> configuration;
    private readonly ILogger<KnowledgeGraphTools> logger;
    private readonly ITreeConfigurationStoreProvider store;

    public KnowledgeGraphTools(IAppSettingService appSettingService,ILogger<KnowledgeGraphTools> logger, ITreeConfigurationStoreProvider store)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "KnowledgeGraph.json");
        var json = File.ReadAllText(jsonPath);
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var entries = JsonSerializer.Deserialize<List<GraphEntry>>(json, options) ?? [];

        foreach (var entry in entries)
        {
            graph[entry.Node] = entry.Relationships;
            nodeFilters[entry.Node.Name] = entry.Filters;
        }

        var indexBuilder = new StringBuilder();
        foreach (var node in graph.Keys)
        {
            var generalDesc = node.Descriptions
                .FirstOrDefault(d => d.DescriptionType == DescriptionType.General)?.Text ?? string.Empty;
            indexBuilder.AppendLine($"{node.Name}|{string.Join(",", node.Tags)}|{generalDesc}");
        }
        CompactNodeIndex = indexBuilder.ToString();
        configuration = appSettingService.Configuration;
        this.logger = logger;
        this.store = store;
    }

    private (List<string> Matches, string Residual) MatchNodeNamesByText(string query)
    {
        query = query.Replace(",", " ").Replace(";", " ").Replace("  ", " ").Trim();

        var searchTerms = new List<(string Term, string NodeName)>();
        foreach (var node in graph.Keys)
        {
            searchTerms.Add((node.Name, node.Name));
            foreach (var tag in node.Tags)
                searchTerms.Add((tag, node.Name));
        }

        searchTerms = searchTerms
            .DistinctBy(t => (t.Term.ToLowerInvariant(), t.NodeName.ToLowerInvariant()))
            .OrderByDescending(t => t.Term.Split(' ').Length)
            .ThenByDescending(t => t.Term.Length)
            .ToList();

        var remainingQuery = query;
        var matchedNodeNames = new List<string>();

        foreach (var (term, nodeName) in searchTerms)
        {
            if (remainingQuery.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                if (!matchedNodeNames.Contains(nodeName, StringComparer.OrdinalIgnoreCase))
                    matchedNodeNames.Add(nodeName);
                remainingQuery = remainingQuery.Replace(term, " ", StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        var residual = string.Join(' ', remainingQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2));

        return (matchedNodeNames, residual);
    }

    private async Task<List<string>> MatchNodeNamesByLlmAsync(string query)
    {
        var systemPrompt = $"""
            # Node Matcher
            Return ONLY the node names from the index that match the user query.
            Match based on node name, tags, AND description semantics.
            In some cases, the query might have customer name like walmart, adobe etc. in those cases you need to replace them as customer.
            Similarly in some cases you might have product name like dynamics 365, office 365, azure eventhub, azure service bus etc. in those cases you need to replace them as product.
            in some case node implicitly support trend,chart,metric but might not have tag for it , in those case return the closest nodes
            Index format: nodeName|tag1,tag2,...|description
            Return node names as a JSON string array. Return [] if no match.
            Do NOT wrap the JSON in markdown code fences. Output ONLY the raw JSON array.

            {CompactNodeIndex}
            """;

        //string endpoint = SecretManager.GetAzureOpenAIRoleBaseAccessControl();
        string endpoint = configuration[AppSettingConstants.Configuration_AzureOpenAIEndpoint];
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        var chatClient = client
            //.GetChatClient("gpt-5.2")
            .GetChatClient("gpt-4o")
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(options =>
            {
                options.Temperature = 0f;
                options.Seed = 42;
            })
            .Build();
        ChatClientAgent agent = chatClient.AsAIAgent(instructions: systemPrompt);

        //var session = await agent.CreateSessionAsync();
        AgentResponse<List<string>> response = default!;
        for (int attempt = 1; attempt <= 3; attempt++)
        { 
            try
            {
                logger.LogInformation("Calling KnowledgeGraph.MatchNodesByLlm (attempt {Attempt}) with query: {Query}", attempt, query);
                response = await agent.RunAsync<List<string>>(query);
                logger.LogInformation("Called KnowledgeGraph.MatchNodesByLlm (attempt {Attempt}), here is response: [{Nodes}]",
                    attempt, string.Join(", ", response.Result ?? []));
                if(response.Result != null && response.Result.Count!=0)
                    break;
            } catch (Exception ex)
            {
                logger.LogError("KnowledgeGraph.MatchNodesByLlm failed (attempt {Attempt}): {Error}", attempt, ex.Message);
            }
        }


        List<string> llmMatches;
        try
        {
            llmMatches = response.Result ?? [];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var text = response.Text ?? string.Empty;
            llmMatches = ParseJsonArrayFromText(text);
            logger.LogError($"Knowledge graph:{ex.Message}", ex);

        }

        return llmMatches
            .Where(name => FindNode(name) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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


   
    private async Task<Dictionary<string, bool>> ValidateNodeFiltersAsync(string query, List<string> matchedNodeNames)
    {
        var filterContext = new StringBuilder();
        foreach (var nodeName in matchedNodeNames)
        {
            filterContext.AppendLine($"Node: {nodeName}");
            if (nodeFilters.TryGetValue(nodeName, out var filters) && filters.Count > 0)
            {
                foreach (var kvp in filters)
                    filterContext.AppendLine($"  Filter: {kvp.Key} - {kvp.Value}");
            }
            else
            {
                filterContext.AppendLine("  No filters available");
            }
        }

        var systemPrompt = $"""
            # Filter Validator
            Given the user query and each node's supported filters, determine if each node supports the filters the user is asking for.
            If the user query has no filter intent, return true for all nodes.
            If a node has no filters available and the user query has filter intent, return false for that node.
            IMPORTANT: Date ranges, start date, end date, time periods (e.g., "last 30 days", "from January to March") are NOT filters. Ignore them when evaluating filter support.
            Return a JSON array of objects, each with "Key" (node name) and "Value" (true/false).
            Do NOT wrap the JSON in markdown code fences. Output ONLY the raw JSON array.

            {filterContext}
            """;

        string endpoint = configuration[AppSettingConstants.Configuration_AzureOpenAIEndpoint];
        var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
        var chatClient = client
            .GetChatClient("gpt-4o")
            .AsIChatClient()
            .AsBuilder()
            .ConfigureOptions(options =>
            {
                options.Temperature = 0f;
                options.Seed = 42;
            })
            .Build();
        ChatClientAgent agent = chatClient.AsAIAgent(instructions: systemPrompt);

        AgentResponse<List<KeyValuePair<string, bool>>> response = default!;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                logger.LogInformation("Calling KnowledgeGraph.ValidateNodeFilters (attempt {Attempt}) with query: {Query}, nodes: [{Nodes}]",
                    attempt, query, string.Join(", ", matchedNodeNames));
                response = await agent.RunAsync<List<KeyValuePair<string, bool>>>(query);
                logger.LogInformation("Called KnowledgeGraph.ValidateNodeFilters (attempt {Attempt}), here is response: {Result}",
                    attempt, JsonSerializer.Serialize(response.Result));
                if (response.Result != null && response.Result.Count != 0)
                    break;
            }
            catch (Exception ex)
            {
                logger.LogError("KnowledgeGraph.ValidateNodeFilters failed (attempt {Attempt}): {Error}", attempt, ex.Message);
            }
        }

        try
        {
            return response.Result?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                ?? matchedNodeNames.ToDictionary(n => n, _ => true, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var text = response.Text ?? string.Empty;
            var parsed = ParseJsonDictionaryFromText(text);
            logger.LogError($"Knowledge graph filter validation parse error: {ex.Message}", ex);
            return parsed.Count > 0 ? parsed : matchedNodeNames.ToDictionary(n => n, _ => true, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, bool> ParseJsonDictionaryFromText(string text)
    {
        var cleaned = text.Replace("```json", "").Replace("```", "").Trim();
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var jsonFragment = cleaned[start..(end + 1)];
            try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(jsonFragment) ?? []; }
            catch { }
        }
        return [];
    }

    private async Task<List<string>> MatchNodeNamesAsync(string query)
    {
        //var (textMatches, residual) = MatchNodeNamesByText(query);

        //if (textMatches.Count == 0)
        return (await MatchNodeNamesByLlmAsync(query)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        //if (!string.IsNullOrWhiteSpace(residual))
        //{
        //    var llmMatches = await MatchNodeNamesByLlmAsync(query);
        //    return textMatches.Union(llmMatches, StringComparer.OrdinalIgnoreCase).ToList();
        //}

        //return textMatches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Node? FindNode(string name) =>
        graph.Keys.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string BuildKnowledge(List<string> matchedNodeNames, Func<Description, bool> descriptionFilter, Dictionary<string, bool>? filterValidation = null)
    {
        // Sort so nodes with relationships (source nodes) are processed first.
        // This ensures relationship targets are expanded under the source node
        // rather than appearing as separate top-level entries.
        var sortedNodeNames = matchedNodeNames
            .OrderByDescending(name =>
            {
                var n = FindNode(name);
                return n is not null && graph.TryGetValue(n, out var rels) ? rels.Count : 0;
            })
            .ToList();

        var described = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        builder.AppendLine("# Domain Knowledge");

        foreach (var nodeName in sortedNodeNames)
        {
            var node = FindNode(nodeName);
            if (node is null || !described.Add(node.Name))
                continue;

            builder.AppendLine();
            builder.AppendLine($"## {node.Name}");
            builder.AppendLine($"- **Also known as:** {string.Join(", ", node.Tags)}");

            var nodeDescs = node.Descriptions.Where(descriptionFilter).ToList();
            if (nodeDescs.Count > 0)
            {
                builder.AppendLine($"- **Details:**");
                foreach (var desc in nodeDescs)
                {
                    if (desc.DescriptionType == DescriptionType.System && filterValidation != null && filterValidation.TryGetValue(node.Name, out var areFiltersSupported) && !areFiltersSupported)
                    {
                        builder.AppendLine($"  - use NLTKqlSkill to fetch data.");
                    }
                    else
                    {
                        builder.AppendLine($"  - {desc.Text}");
                    }
                }
            }

            if (graph.TryGetValue(node, out var relationships) && relationships.Count > 0)
            {
                builder.AppendLine($"- **Relationships:**");
                foreach (var rel in relationships)
                {
                    builder.AppendLine($"  - [{string.Join(", ", rel.Relations)}] **{rel.Node}**");
                    if (described.Add(rel.Node))
                    {
                        var relatedNode = FindNode(rel.Node);
                        if (relatedNode is not null)
                        {
                            builder.AppendLine($"    - **Also known as:** {string.Join(", ", relatedNode.Tags)}");
                            var relDescs = relatedNode.Descriptions.Where(descriptionFilter).ToList();
                            if (relDescs.Count > 0)
                            {
                                builder.AppendLine($"    - **Details:**");
                                foreach (var desc in relDescs)
                                    builder.AppendLine($"      - {desc.Text}");
                            }
                        }
                    }
                }
            }
        }

        return builder.ToString();
    }

    public async Task<string> GetSystemKnowledgeAsync(string query)
    {
        
        var matchedNodeNames = await MatchNodeNamesAsync(query);
        var filterValidation = await ValidateNodeFiltersAsync(query, matchedNodeNames);
        return BuildKnowledge(matchedNodeNames, _ => true, filterValidation);
    }

    public async Task<string> GetGeneralKnowledgeAsync(string query)
    {
        var matchedNodeNames = await MatchNodeNamesAsync(query);
        var filterValidation = await ValidateNodeFiltersAsync(query, matchedNodeNames);
        return BuildKnowledge(matchedNodeNames, d => d.DescriptionType == DescriptionType.General, filterValidation);
    }
}

public class GraphEntry
{
    public Node Node { get; set; } = new();
    public Dictionary<string, string> Filters { get; set; } = [];
    public List<Relationship> Relationships { get; set; } = [];
}

public enum DescriptionType { General, System }

public class Description
{
    public DescriptionType DescriptionType { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class Node
{
    public string Name { get; set; } = string.Empty;
    //public string? AspectName { get; set; }
    public List<Description> Descriptions { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public class Relationship
{
    public string Node { get; set; } = string.Empty;
    public List<string> Relations { get; set; } = [];
}
