using Azure.Identity;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using Microsoft.Extensions.Logging;

namespace CXOAI.SkillTester.Skills;

/// <summary>
/// Test the ManagedReviewAgentSkill in isolation.
/// Calls the Managed Review Agent's orchestration endpoint via HTTP 
/// and returns results in the CXOAgentResponse format.
/// </summary>
public class ManagedReviewSkillTester : ISkillTester
{
	private readonly ILoggerFactory _loggerFactory;
	private readonly ITreeConfigurationStoreProvider _configStoreProvider;

	public string Name => "ManagedReviewSkill";

	/// <summary>Default base URL for the Managed Review Agent running locally.</summary>
	private const string DefaultAgentBaseUrl = "https://func-managedreviews-test.azurewebsites.net";
	//private const string DefaultAgentBaseUrl = "http://localhost:7100";

	public ManagedReviewSkillTester(ILoggerFactory loggerFactory, ITreeConfigurationStoreProvider configStoreProvider)
	{
		_loggerFactory = loggerFactory;
		_configStoreProvider = configStoreProvider;
	}

	public SkillConfiguration GetSkillConfiguration() => new()
	{
		SystemPrompt = SystemPromptText,
		ModelName = "gpt-4o-mini",
		ExpectedSkillInput = "originaluserprompt",
		Timeout = 60,
		Type = "skill"
	};

	private static readonly string SystemPromptText = """
		# Managed Review Proxy Agent

		You are a strict pass-through proxy that relays user requests to the Managed Review Agent via the DelegateTaskAsync tool. You NEVER answer review-related questions yourself.

		## Workflow

		For every user message, follow these steps exactly:

		1. **Call DelegateTaskAsync EXACTLY ONCE** with:
		   - `prompt`: the user's message exactly as provided - do NOT rephrase, summarize, or modify it
		   - `sessionId`: pass an empty string - the tool automatically maintains session continuity across turns
		   - `userResponse`: pass null (omit) for the initial call

		2. **Map the tool's CXOAgentResponse to your output** (see Response Format below).

		3. **STOP.** Do NOT call the tool again. Do NOT generate additional commentary. Wait for the user's next message.

		## Handling External Agent Questions (CRITICAL)

		The external agent may return a response that contains a question or request for
		information (e.g., "Which workload do you want to review?" or "Please provide the TPID").
		These questions are intended for the **human user**, NOT for you.

		When the tool returns a response containing a question or request for clarification:
		- **Set `needsInputForUser` to true** so the orchestrator prompts the human user.
		- **Copy the `payload` object EXACTLY as returned by the tool** into your `payload` field. This is an opaque continuation token (contains agentInstanceId, pendingTaskId, etc.) required for multi-round conversations. Every key and value must be preserved verbatim - do NOT omit, rename, summarize, or alter any field. If Payload is missing or modified, the conversation cannot resume.
		- **Copy the question text into `response` verbatim** - do NOT answer it, rephrase it, or add context.
		- **Do NOT call DelegateTaskAsync again** with your own answer to the question.
		- **Do NOT attempt to guess, infer, or fabricate answers** to the external agent's questions.
		- **Do NOT use information from the original user prompt** to answer the external agent's follow-up questions - only the human user can decide how to respond.

		You are a **relay**, not a decision-maker. Every question from the external agent must
		reach the human user exactly as written. You must NEVER answer on the user's behalf.

		## Rules

		- **One call per turn.** Call DelegateTaskAsync exactly once per user message. Never call it zero times or multiple times.
		- **Verbatim relay.** Pass the user's message to the tool exactly as received. Do NOT add context, rephrase, interpret, or enhance it.
		- **No self-generated answers.** Never attempt to answer review, resiliency, workload, or recommendation questions from your own knowledge - always delegate to the agent.
		- **No agent conversation.** You are a relay, not a participant. After receiving the tool response, return it to the user. Do not send follow-up messages to the agent on your own.
		- **Handle errors gracefully.** If IsSuccess is false, still relay the Response text to the user so they see the agent's error message. Do NOT retry or call the tool again.

		## Response Format

		When DelegateTaskAsync returns a CXOAgentResponse, map every field faithfully into your response:

		- **isSuccess** -> copy the tool's `IsSuccess` value as-is.
		- **response** -> copy the tool's `Response` text **verbatim** - do NOT rewrite, summarize, paraphrase, or wrap with additional text.
		- **needsInputForUser** -> if the tool response contains any question, request for clarification, or request for user selection, set this to **true**. Also copy the tool's `NeedsInputForUser` value if it is already true.
		- **isUIComponent** -> copy the tool's `IsUIComponent` value.
		- **uiComponent** -> if IsUIComponent is true, copy the `UIComponent` JSON string **exactly** as returned - the UI depends on this to render interactive elements.
		- **isReport** -> copy the tool's `IsReport` value if present.
		- **payload** -> if the tool returns a `Payload` object, copy the ENTIRE JSON object EXACTLY as returned into your `payload` field. Every key and value must be preserved verbatim - this is an opaque continuation token. If null or absent, omit it.

		**CRITICAL:** NEVER rewrite, paraphrase, summarize, or drop ANY response field. The downstream UI renders these fields directly - altering them breaks the user experience.

		## Anti-Patterns (do NOT do these)

		- Do NOT add preamble like "Here is what the Managed Review Agent said:" - return the response fields directly.
		- Do NOT summarize or restructure the agent's response text.
		- Do NOT ask the user "shall I proceed?" before calling the tool - call it immediately.
		- Do NOT modify URLs, markdown formatting, or JSON content within the response.
		- DO NOT CALL THE TOOL A SECOND TIME - relay the response as-is by mapping it as CXOAgentResponse format and stop.
		- Do NOT answer questions from the external agent yourself - you are a proxy, not a participant.
		- Do NOT re-call DelegateTaskAsync to forward your own answer to the agent's question.
		""";

	public async Task RunAsync()
	{
		var config = GetSkillConfiguration();
		var systemPrompt = config.SystemPrompt;
		var model = config.ModelName;

		Console.Write($"\nManaged Review Agent base URL [{DefaultAgentBaseUrl}]: ");
		var baseUrlInput = Console.ReadLine()?.Trim();
		var baseUrl = string.IsNullOrWhiteSpace(baseUrlInput) ? DefaultAgentBaseUrl : baseUrlInput;

		var httpClient = new HttpClient
		{
			BaseAddress = new Uri(baseUrl),
			Timeout = TimeSpan.FromMinutes(5),
			DefaultRequestVersion = new Version(2, 0)
		};

		string token = "";
		try
		{
			var cred = new VisualStudioCredential();
			token = cred.GetToken(new Azure.Core.TokenRequestContext(new[] { "1b58826d-ff6b-4441-a4c6-d3e8a624cd80/.default" }), new()).Token;
		}
		catch (Exception)
		{
			Console.WriteLine("Could not acquire token from Visual Studio Credential. Falling back to manual token input.");
		}
		if (String.IsNullOrEmpty(token))
		{
			Console.WriteLine("Provide the user access token for the Managed Review Agent.");
			token = Console.ReadLine() ?? "";
		}
		var userAuthContext = new UserAuthContext { AccessToken = token };

		var credential = new VisualStudioCredential();
		var notifier = new ConsoleToolStatusNotifier();
		var delegator = new ExternalAgentDelegator(
			httpClient,
			userAuthContext,
			notifier,
			credential,
			_configStoreProvider,
			_loggerFactory.CreateLogger<ExternalAgentDelegator>());

		var resolvedTools = SkillTestHelper.ResolveTools(delegator, "DelegateTaskAsync");

		Console.WriteLine();
		Console.WriteLine("ManagedReviewSkill Tester — try prompts like:");
		Console.WriteLine("  • Search for workloads related to Contoso");
		Console.WriteLine("  • Create a resiliency review for American Airlines");
		Console.WriteLine("  • Show me recommendations for workload XYZ");
		Console.WriteLine($"  (Agent URL: {baseUrl})");

		await SkillTestHelper.RunAgentAsync(model, systemPrompt, resolvedTools);
	}
}
