using System.Reflection;
using Azure.AI.OpenAI;
using Azure.Identity;
using CXOAI.SkillFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CXOAI.SkillTester;

/// <summary>
/// Shared helpers for skill testers � tool resolution and agent prompt loop.
/// </summary>
public static class SkillTestHelper
{
    private static readonly string UserInputProtocol = $"""

        ## User Input Protocol
        If you cannot complete the task because a required parameter is missing
        (e.g., a tool returned an error about a missing value), respond with
        EXACTLY this format:
        {CXOAgentResponse.NeedInputMarker} <your question to the user>

        If you have everything you need, respond normally with your result.
        Do NOT include {CXOAgentResponse.NeedInputMarker} when you have a complete answer.
        """;

    /// <summary>
    /// Resolve specific public methods from a tool instance into <see cref="AITool"/> list.
    /// </summary>
    public static List<AITool> ResolveTools(object toolInstance, params string[] methodNames)
    {
        var tools = new List<AITool>();
        var type = toolInstance.GetType();

        foreach (var methodName in methodNames)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found on '{type.Name}'.");

            tools.Add(AIFunctionFactory.Create(method, toolInstance));
        }

        return tools;
    }

    /// <summary>
    /// Interactive prompt loop: asks for user input, runs the agent, displays results.
    /// Supports multi-round user input when the agent requests missing parameters.
    /// Temperature and seed can be overridden via env vars SKILLTESTER_TEMPERATURE and SKILLTESTER_SEED.
    /// Defaults: temperature=0 (deterministic), seed=42.
    /// </summary>
    public static async Task RunAgentAsync(string model, string systemPrompt, List<AITool> tools)
    {
        var temperature = float.TryParse(Environment.GetEnvironmentVariable("SKILLTESTER_TEMPERATURE"), out var t) ? t : 0f;
        var seed = long.TryParse(Environment.GetEnvironmentVariable("SKILLTESTER_SEED"), out var s) ? s : 42L;

        while (true)
        {
            Console.Write("\nEnter your prompt (or 'back' to return): ");
            var prompt = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Equals("back", StringComparison.OrdinalIgnoreCase))
                return;

            string endpoint = SecretManager.GetAzureOpenAIRoleBaseAccessControl();
            var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
            ChatClientAgent agent = client
                .GetChatClient(model)
                .AsIChatClient()
                .AsBuilder()
                .ConfigureOptions(options => { options.Temperature = temperature; options.Seed = seed; })
                .Build()
                .AsAIAgent(instructions: systemPrompt + UserInputProtocol, tools: tools);

            var currentPrompt = prompt;
            var round = 0;

            while (true)
            {
                round++;
                try
                {
                    var response = await agent.RunAsync<CXOAgentResponse>(currentPrompt);
                    var result = response.Result;

                    Console.WriteLine($"  [Tokens] Prompt: {response.Usage?.InputTokenCount}, " +
                        $"Completion: {response.Usage?.OutputTokenCount}, " +
                        $"Total: {response.Usage?.TotalTokenCount}");

                    if (result.NeedsInputForUser)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [Round {round}] Agent needs input: {result.Response}");
                        if(result.IsUIComponent && !string.IsNullOrEmpty(result.UIComponent))
                        {
                            Console.WriteLine($"  [Round {round}] UI Component: {result.UIComponent}");
                        }
                        Console.ResetColor();
                        Console.Write("  Your response (or 'skip'): ");
                        var userInput = Console.ReadLine() ?? "";
                        if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("skip", StringComparison.OrdinalIgnoreCase))
                            break;
                        currentPrompt = $"{prompt}\n\n## Agent needs input: {result.Response}: {result.UIComponent}  \n\n## User Response (round {round})\n{userInput}";
                    }
                    else
                    {
                        if (result.IsUIComponent && !string.IsNullOrEmpty(result.UIComponent))
                        {
                            var componentType = "unknown";
                            try
                            {
                                var jo = Newtonsoft.Json.Linq.JObject.Parse(result.UIComponent);
                                componentType = jo["componentType"]?.ToString() ?? "unknown";
                            }
                            catch { }

                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"\n  [UIComponent] isUIComponent=true, componentType={componentType}");
                            Console.ResetColor();
                            Console.WriteLine(result.UIComponent);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n  [Result] ({result.Response.Length} chars)");
                            Console.ResetColor();
                            Console.WriteLine(result.Response);
                        }
                        break;
                    }
                }
                catch (ToolParameterException tpe)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [Round {round}] Tool parameter missing: {tpe.ToPromptMessage()}");
                    Console.ResetColor();
                    Console.Write("  Your response (or 'skip'): ");
                    var userInput = Console.ReadLine() ?? "";
                    if (string.IsNullOrWhiteSpace(userInput) || userInput.Equals("skip", StringComparison.OrdinalIgnoreCase))
                        break;
                    currentPrompt = $"{prompt}\n\n## User Response (round {round})\n{userInput}";
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [Error] {ex.GetType().Name}: {ex.Message}");
                    Console.ResetColor();
                    break;
                }

                if (round >= 5)
                {
                    Console.WriteLine("  Max rounds (5) reached.");
                    break;
                }
            }
        }
    }
}
