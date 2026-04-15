// ──────────────────────────────────────────────────────────────────────────────
// UX GENERATOR TOOL
// [UX_GENERATOR_SKILL]
// Scope : Section 2 – Full E2E integration (UI + deployed API)
//
// This is the ONLY tool exposed to UXGeneratorSkill.
// The LLM agent (driven by the decision matrix in Skills.json) determines the
// correct ComponentType and builds PropsJson from upstream AspectSkill /
// NLTKqlSkill data, then calls GenerateComponentAsync ONCE.
//
// Wire format sent to UI:
//   CXOAgentResponse.Response  = JSON { "text": "...", "component": { componentType, title, props } }
//   CXOAgentResponse.UIComponent = JSON { componentType, title, props }   (for future orchestrator use)
//
// ──────────────────────────────────────────────────────────────────────────────

using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace CXOAI.Tools;

// ── Input model ───────────────────────────────────────────────────────────────

/// <summary>
/// Input built by the LLM from upstream skill data before calling
/// <see cref="UXGeneratorTool.GenerateComponentAsync"/>.
/// </summary>
[Description("Input for GenerateComponentAsync. The LLM must populate all three fields from the upstream data before calling the tool.")]
public class UXComponentInput
{
    /// <summary>Fluent UI v8 component type selected from the decision matrix.</summary>
    [Description(
        "The Fluent UI v8 component type selected from the decision matrix. " +
        "Must be one of: LineChart, BarChart, HorizontalBarChart, StackedBarChart, AreaChart, " +
        "PieChart, DonutChart, KpiTiles, DetailsList, GroupedList, Pivot, Accordion, Dropdown, " +
        "MessageBar, OnePageLayout")]
    public string ComponentType { get; set; } = string.Empty;

    /// <summary>Short descriptive title shown above the rendered component.</summary>
    [Description("A concise descriptive title shown above the component, " +
                 "e.g. 'Walmart CSAT Trend – Last 6 Months' or 'Top-5 Open P1 Incidents'.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Valid JSON string whose shape must match the props schema for the chosen
    /// ComponentType (see decision matrix in UXGeneratorSkill system prompt).
    /// </summary>
    [Description("Valid JSON string containing the component-specific props built from the upstream data. " +
                 "Shape must match the props schema for the chosen ComponentType.")]
    public string PropsJson { get; set; } = string.Empty;
}

// ── Tool class ────────────────────────────────────────────────────────────────

/// <summary>
/// Validates the LLM-built component spec and packages it into a
/// <see cref="CXOAgentResponse"/> with <c>IsUIComponent = true</c>.
/// </summary>
public class UXGeneratorTool : ToolBase
{
    private readonly ILogger<UXGeneratorTool> _logger;

    // Keep in sync with the decision matrix in UXGeneratorSkill system prompt (Skills.json / SeedData.json).
    private static readonly HashSet<string> ValidComponentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "LineChart", "BarChart", "HorizontalBarChart", "StackedBarChart", "AreaChart",
        "PieChart", "DonutChart", "KpiTiles", "DetailsList", "GroupedList",
        "Pivot", "Accordion", "Dropdown", "MessageBar",
        "OnePageLayout", "SummaryText"
    };

    public UXGeneratorTool(ILogger<UXGeneratorTool> logger, IToolStatusNotifier notifier) : base(notifier)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the component spec and returns a <see cref="CXOAgentResponse"/>
    /// with <c>IsUIComponent = true</c>.
    /// <para>
    /// Call ONLY ONCE after the LLM has determined <see cref="UXComponentInput.ComponentType"/>
    /// from the decision matrix and built <see cref="UXComponentInput.PropsJson"/> from
    /// the upstream skill data.
    /// </para>
    /// </summary>
    [Description(
        "Validates and packages the UI component spec into a CXOAgentResponse with IsUIComponent=true. " +
        "Call ONLY ONCE after determining ComponentType from the decision matrix and building " +
        "PropsJson from upstream data. Do NOT call multiple times.")]
    public async Task<CXOAgentResponse> GenerateComponentAsync(
        [Description("The component type, title, and props JSON built from upstream skill data.")] UXComponentInput input)
    {
        _logger.LogInformation(
            "UXGeneratorTool.GenerateComponentAsync: componentType={ComponentType}, title={Title}",
            input.ComponentType, input.Title);

        await NotifyAsync($"🔍 Building {input.ComponentType}: {input.Title}");

        // ── Validate component type ───────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(input.ComponentType) || !ValidComponentTypes.Contains(input.ComponentType))
        {
            _logger.LogWarning("UXGeneratorTool: Invalid ComponentType '{ComponentType}'", input.ComponentType);
            var validList = string.Join(", ", ValidComponentTypes.OrderBy(t => t));
            var typeError = new CXOAgentResponse
            {
                IsSuccess = false,
                NeedsInputForUser = false,
                IsUIComponent = false,
                Response = $"Invalid component type '{input.ComponentType}'. Supported types: {validList}."
            };
            await NotifyAsync("❌ unable to build {input.ComponentType}: {input.Title}");
            throw new ToolParameterException(JsonConvert.SerializeObject(typeError));
        }

        // ── Parse and validate PropsJson ──────────────────────────────────────
        // LLMs sometimes append trailing braces or whitespace after valid JSON.
        // Use JsonTextReader to parse only the first complete JSON object and
        // ignore any trailing content.
        JObject propsObject;
        try
        {
            if (string.IsNullOrWhiteSpace(input.PropsJson))
            {
                propsObject = new JObject();
            }
            else
            {
                using var reader = new JsonTextReader(new System.IO.StringReader(input.PropsJson))
                {
                    SupportMultipleContent = true   // don't throw on trailing text
                };
                propsObject = JObject.Load(reader);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "UXGeneratorTool: Invalid PropsJson for componentType={ComponentType}", input.ComponentType);
            var propsError = new CXOAgentResponse
            {
                IsSuccess = false,
                NeedsInputForUser = false,
                IsUIComponent = false,
                Response = $"Invalid PropsJson for '{input.ComponentType}': {ex.Message}. Provide valid JSON."
            };
            await NotifyAsync("❌ Unable to build {input.ComponentType}: {input.Title}");

            throw new ToolParameterException(JsonConvert.SerializeObject(propsError));
        }

        // ── Build component spec (UIComponent wire format) ────────────────────
        var componentSpec = new JObject
        {
            ["componentType"] = input.ComponentType,
            ["title"]         = input.Title ?? string.Empty,
            ["props"]         = propsObject
        };
        var uiComponentJson = componentSpec.ToString(Formatting.None);

        // Response MUST be a plain text label — NOT a nested JSON string.
        // If Response contains a nested JSON with a "component" key, the LLM may
        // expand it into CXOAgentResponse.Payload.component, which fails STJ
        // deserialization (Newtonsoft JObject? is not a System.Text.Json type).
        var responseLabel = $"Generated {input.ComponentType}: {input.Title}";

        _logger.LogInformation(
            "UXGeneratorTool: Successfully built {ComponentType} spec ({PropsBytes} chars props)",
            input.ComponentType, uiComponentJson.Length);

        await NotifyAsync($"✅ {input.ComponentType} ready");

        return new CXOAgentResponse
        {
            IsSuccess         = true,
            NeedsInputForUser = false,
            IsUIComponent     = true,
            UIComponent       = uiComponentJson,   // JSON-encoded component spec
            Response          = responseLabel        // plain text — no nested JSON
        };
    }
}