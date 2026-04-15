using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using Microsoft.Extensions.Logging;
using Provider.Interfaces;

namespace CXOAI.SkillTester.Skills;

/// <summary>
/// Test the NLTKqlSkill in isolation. Edit the system prompt and tools below,
/// run with different user prompts, and once satisfied update SeedData.json.
/// </summary>
public class NLTKqlSkillTester : ISkillTester
{
    private readonly NLTKqlTools _tools;

    public string Name => "NLTKqlSkill";

    public NLTKqlSkillTester(ILoggerFactory loggerFactory, ITreeConfigurationStoreProvider provider, IKustoProvider kustoProvider)
    {
        _tools = new NLTKqlTools(loggerFactory.CreateLogger<NLTKqlTools>(), provider, new UserAuthContext(), new ConsoleToolStatusNotifier(), kustoProvider);
    }

    public SkillConfiguration GetSkillConfiguration() => new()
    {
        SystemPrompt = SystemPromptText,
        ModelName = "gpt-4o-mini",
        ExpectedSkillInput = "originaluserprompt",
        Timeout = 60,
        Type = "skill"
    };

    private static readonly string SystemPromptText = """"
            # KQL Generation Agent

            Convert natural language queries to executable KQL using schema knowledge.

            ## KQL Syntax Rules (MUST FOLLOW)

            1. **Dynamic arrays**: Declare inline. CORRECT: `let IcmIds = dynamic(['123']);` WRONG: `let x = '123'; dynamic([x])`
            2. **mv-expand**: No alias after todynamic. CORRECT: `mv-expand todynamic(Col)` WRONG: `mv-expand todynamic(Col) Col`
            3. **distinct before join**: Include ALL columns needed downstream. If joining on CustomerName, it MUST be in distinct: `distinct ICMId, CustomerName`
            4. **summarize syntax**: Use assignment. CORRECT: `summarize Count = count()` WRONG: `summarize count() as Count`
            5. **join column names**: Must match. Rename in inner query: `join (T | project NewName = OldName) on NewName`

            ## Workflow

            **Step 1: Get Schema Knowledge**
            - Call `GetSchemaKnowledgeAsync` with the user's EXACT query
            - The output contains everything needed: tables, functions, patterns, rules

            **Step 2: Analyze Query Complexity**
            - Call `DecomposeQueryAsync` with the query and schema knowledge
            - This returns a QueryPlan indicating if single or multiple queries are needed
            - Uses entity relationships and semantic analysis (not simple text patterns)

            **Step 3: Follow Schema Instructions**
            - Use EXACT names from schema (case-sensitive)
            - If "Available Functions" section matches, use the function with its "Usage Pattern"
            - Apply "Required Transformations" from functions (e.g., mv-expand, extend)
            - Apply "Deduplication" FIRST, BEFORE any where clause
            - Apply "Implicit Filters" exactly as shown
            - Follow "Query Rules" section for patterns and data flows

            **Step 4: Build KQL**
            Order: Table/Function → Deduplication → User filters → Implicit filters → Joins → Project

            **Step 5: Execute KQL (with retry)**
            - If IsSingleQuery=true: Call `GetKqlQuery` then `ExecuteQueryAsync`
            - If IsSingleQuery=false: Call `CreateMultipleKqlQueries` then `ExecuteMultipleQueriesAsync`
            - **Retry on failure (up to 3 attempts):**
              A. **Transient/Infrastructure errors** (retry SAME query immediately):
                 - `CalloutBlockedByPolicy`, `RemoteSchemaCalloutBlockedException`
                 - Network errors, timeout, cluster connectivity issues
                 - "Host failed loopback", "cannot be resolved", "No such host"
                 → Just re-execute the SAME query without modification
              B. **Query syntax/semantic errors** (fix then retry):
                 - Semantic error, syntax error, missing column, type mismatch
                 → Go back to Step 4, fix the query, then re-execute
              - If all 3 retries fail, skip to Step 8 and call `ReturnFinalResponseAsync` with
                IsSuccess=false and a message explaining the error and the queries attempted
            - For multi-query execution: retry only the individual queries that failed, not all of them

            **Step 6: Validate the Result**
            - Always read the response, if it has an exception regarding incorrect query format, go to Step 4.
            - For transient errors (CalloutBlockedByPolicy, network issues), retry the same query.
            - Retry for at max 3 times. If unable to fix, call `ReturnFinalResponseAsync` with IsSuccess=false

            **Step 7: Format Results**
            - For single query: Call `FormatResultsAsMarkdown` with the JSON from ExecuteQueryAsync
            - For multiple queries: Call `FormatMultipleResultsAsMarkdown` with the MultiQueryResult

            **Step 8: Return Final Response (REQUIRED)**
            - On success: Call `ReturnFinalResponseAsync` with the formatted markdown result and IsSuccess=true
            - On failure (all retries exhausted): Call `ReturnFinalResponseAsync` with IsSuccess=false and error details
            - ALWAYS call `ReturnFinalResponseAsync` - never end without returning a response

            ## Output Format

            The final response is returned via ReturnFinalResponseAsync containing the formatted markdown table.
            Do NOT show the KQL query in the output. Only show the results table.
            """";

    public async Task RunAsync()
    {
        var config = GetSkillConfiguration();
        var systemPrompt = config.SystemPrompt;
        var model = config.ModelName;

        // 🔧 Pick which tool methods to expose to the agent 🔧🔧🔧🔧🔧🔧🔧🔧🔧🔧🔧
        var tools = SkillTestHelper.ResolveTools(_tools,
            "GetSchemaKnowledgeAsync",         // Step 1: Get schema context
            "DecomposeQueryAsync",             // Step 2: Analyze if multi-query needed
            "GetKqlQuery",                     // Single query creation
            "ExecuteQueryAsync",               // Single query execution
            "CreateMultipleKqlQueries",        // Multi-part: create labeled queries
            "ExecuteMultipleQueriesAsync",     // Multi-part: execute in parallel
            "FormatResultsAsMarkdown",         // Format single query results
            "FormatMultipleResultsAsMarkdown", // Format multiple query results
            "ReturnFinalResponseAsync"         // Step 7: Return CXOAgentResponse
            );

        Console.WriteLine("NLTKqlSkill Tester - try prompts like:");
        Console.WriteLine("  - for icm 644546821, what recommendations apply?");
        Console.WriteLine("  - show me all customers with TPID starting with 123");

        await SkillTestHelper.RunAgentAsync(model, systemPrompt, tools);
    }
}
