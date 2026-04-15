using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Text;

namespace CXOAI.Tools;

/// <summary>
/// Reporting tools for generating documents (Word, Excel, PDF) and sending emails.
/// Currently only Word (.docx) export is fully implemented via <see cref="GenerateWordAsync"/>.
/// Other formats (Excel, PDF, Email) are placeholders for future implementation.
/// </summary>
public class ReportingTools : ToolBase
{
    /// <summary>Skill name used for artifact storage keys and download URLs.</summary>
    internal const string SkillName = "ReportingSkill";

    /// <summary>MIME type for Word (.docx) files (Office Open XML).</summary>
    internal const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IArtifactStore? _durableArtifactStore;
    private readonly ILogger<ReportingTools> _logger;
    private readonly ITreeConfigurationStoreProvider _storeProvider;
    private readonly IUserAuthContext _authContext;

    public ReportingTools(ILogger<ReportingTools> logger, ITreeConfigurationStoreProvider storeProvider, IUserAuthContext authContext, IToolStatusNotifier notifier, IArtifactStore? durableArtifactStore = null) : base(notifier)
    {
        _durableArtifactStore = durableArtifactStore;
        _logger = logger;
        _storeProvider = storeProvider;
        _authContext = authContext!;
    }

    [Description("Retrieves a reporting template by document type and template name. Call this first to get the template, then pass it to the appropriate Generate method.")]
    public async Task<string> GetReportingTemplatesAsync(
        [Description("The type of document to generate: Excel, Word, Pdf, or Email.")] ReportingDocumentType reportingDocumentType,
        [Description("The name of the reporting template to retrieve (e.g. 'ExecutiveSummary'). Leave empty for the default template.")] string reportingTemplateName = "")
    {
        _logger.LogInformation("Executing | Tool: ReportingTools | ToolName: GetReportingTemplatesAsync | DocumentType: {DocType}, Template: {Template}",
            reportingDocumentType, reportingTemplateName);
        await NotifyAsync($"📄 Loading {reportingDocumentType} template...");

        var templateName = string.IsNullOrWhiteSpace(reportingTemplateName)
            ? "ExecutiveSummary"
            : reportingTemplateName;

        // Try config store first — allows templates to be managed externally without code changes.
        var stored = await TryGetTemplateFromStoreAsync(reportingDocumentType.ToString(), templateName);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            await NotifyAsync("✅ Template loaded");
            return stored;
        }

        // Fall back to built-in defaults per document type.
        await NotifyAsync("✅ Template loaded");
        return GetDefaultTemplate(reportingDocumentType, templateName);
    }

    // TODO: Implement real Excel generation — out of current scope (Word-only POC).
    [Description("Generates an Excel report from a template and data. Returns an artifact reference with file name, content type, and summary.")]
    public async Task<CXOAgentResponse> GenerateExcelAsync(
        [Description("The Excel template string returned by GetReportingTemplatesAsync.")] string excelTemplate,
        [Description("The data to populate the Excel report with, typically markdown or structured text from upstream skills.")] string textData)
    {
        _logger.LogInformation("Executing | Tool: ReportingTools | ToolName: GenerateExcelAsync");
        var bytes = Encoding.UTF8.GetBytes($"[Excel content based on template: {excelTemplate}]");

        var sb = new StringBuilder();
        sb.AppendLine($"FileName = report.xlsx");
        sb.AppendLine($"ContentType = application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        sb.AppendLine($"SizeBytes = {bytes.Length}");
        sb.AppendLine($"Summary = Excel report generated with data: {textData[..Math.Min(100, textData.Length)]}");
        return new CXOAgentResponse { NeedsInputForUser = false, Response = sb.ToString() };
    }

    [Description("Generates a Word document from a template and markdown content. The caller MUST provide both the template (from GetReportingTemplatesAsync) and the fully composed markdown text in the 'llmProvidesMarkdownText' parameter. Returns an artifact reference with file name, content type, download URL, and summary.")]
    public async Task<CXOAgentResponse> GenerateWordAsync(
        [Description("The Word template string returned by GetReportingTemplatesAsync. Pass the exact string you received from that tool.")] string wordTemplateFromGetReportingTemplatesAsync,
        [Description("REQUIRED. The complete markdown text that YOU (the LLM) must compose using the user's data and the template structure. Write a full markdown document (headings, tables, bullet points, etc.) that fills in every section of the template. This must NOT be empty or null.")] string llmProvidesMarkdownText)
    {
        _logger.LogInformation("Executing | Tool: ReportingTools | ToolName: GenerateWordAsync");
        await NotifyAsync("📝 Generating Word document...");
        _logger.LogDebug("GenerateWordAsync input — Template: [{Template}], MarkdownLength: {Len}",
            wordTemplateFromGetReportingTemplatesAsync?.Length ?? 0,
            llmProvidesMarkdownText?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(llmProvidesMarkdownText))
        {
            _logger.LogWarning("GenerateWordAsync: llmProvidesMarkdownText is empty. " +
                "This usually means the LLM tool call JSON property name did not match during deserialization. " +
                "Template value: [{Template}]", wordTemplateFromGetReportingTemplatesAsync);
            await NotifyAsync("❌ Word document unable to generate");

            return new CXOAgentResponse
            {
                IsSuccess = false,
                NeedsInputForUser = false,
                Response = "GenerateWordAsync failed: llmProvidesMarkdownText is empty."
            };
        }

        var fileName = $"Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}.docx";

        var bytes = WordDocumentBuilder.Build(llmProvidesMarkdownText);
        await NotifyAsync("📦 Storing document...");

        // Store to blob (primary durable store).
        if (_durableArtifactStore is not null)
        {
            try
            {
                var blobUri = await _durableArtifactStore.StoreAsync(fileName, bytes, WordContentType, ct: default);
                _logger.LogInformation("Stored report to blob storage: {BlobUri}", blobUri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store report to blob storage");
                await NotifyAsync("❌ Word document unable to store to artifact storage");
                return new CXOAgentResponse
                {
                    IsSuccess = false,
                    NeedsInputForUser = false,
                    IsUIComponent = false,
                    IsReport = false,
                    Response = $"Failed to store report to blob storage; please try again, in case you continue facing problem please reachout to support team)"
                };
            }
        }

        // Save a local copy to disk for validation / debugging.
        //try
        //{
        //    var localDir = Path.Combine(AppContext.BaseDirectory, "GeneratedReports");
        //    Directory.CreateDirectory(localDir);
        //    var localPath = Path.Combine(localDir, fileName);
        //    await File.WriteAllBytesAsync(localPath, bytes);
        //    _logger.LogInformation("Saved local copy for validation: {LocalPath}", localPath);
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogWarning(ex, "Failed to save local file copy; blob and in-memory artifacts are unaffected.");
        //}

        var downloadUrl = $"/api/artifacts/{fileName}";

        await NotifyAsync("✅ Word document ready for download");

       return new CXOAgentResponse
        {
            IsSuccess = true,
            NeedsInputForUser = false,
            IsUIComponent = false,
            IsReport= true,
            Response = $"[link]({downloadUrl})"
        };
    }

    // TODO: Implement real email sending — out of current scope (Word-only POC).
    [Description("Sends an email with the specified recipient, subject, and body. Returns confirmation with send status.")]
    public async Task<CXOAgentResponse> SendEmailAsync(
        [Description("Recipient email address.")] string to,
        [Description("CC email address (optional).")] string cc = "",
        [Description("Email subject line.")] string subject = "",
        [Description("Email body content in markdown or plain text.")] string body = "")
    {
        _logger.LogInformation("Executing | Tool: ReportingTools | ToolName: SendEmailAsync");
        var sb = new StringBuilder();
        sb.AppendLine($"Send = yes");
        sb.AppendLine($"To = {to}");
        sb.AppendLine($"Subject = {subject}");
        return new CXOAgentResponse { NeedsInputForUser = false, Response = sb.ToString() };
    }

    // TODO: Implement real PDF generation — out of current scope (Word-only POC).
    [Description("Generates a PDF document from a template and data. Returns an artifact reference with file name, content type, and summary.")]
    public async Task<CXOAgentResponse> GeneratePdfAsync(
        [Description("The PDF template string returned by GetReportingTemplatesAsync.")] string pdfTemplate,
        [Description("The data to populate the PDF report with.")] string textData)
    {
        _logger.LogInformation("Executing | Tool: ReportingTools | ToolName: GeneratePdfAsync");
        var bytes = Encoding.UTF8.GetBytes($"[PDF content based on template: {pdfTemplate}]");

        var sb = new StringBuilder();
        sb.AppendLine($"FileName = report.pdf");
        sb.AppendLine($"ContentType = application/pdf");
        sb.AppendLine($"SizeBytes = {bytes.Length}");
        sb.AppendLine($"Summary = PDF document generated with data: {textData[..Math.Min(100, textData.Length)]}");
        return new CXOAgentResponse { NeedsInputForUser = false, Response = sb.ToString() };
    }

    // ═══════════════════════════════════════════════════════════════
    // Template resolution helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts to load a template from the configuration store.
    /// Convention: ComponentName = "ReportingTemplate", ConfigurationName = "{DocType}_{TemplateName}".
    /// Returns null if not found — caller falls back to built-in defaults.
    /// </summary>
    private async Task<string?> TryGetTemplateFromStoreAsync(string docType, string templateName)
    {
        try
        {
            var configName = $"{docType}_{templateName}";
            var configs = await _storeProvider.GetConfigurationsWithNames(
                "ReportingTemplate", [configName], needNestedConfigs: false);
            return configs.FirstOrDefault()?.Configuration;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load template from store: {DocType}/{Template}. Using built-in default.", docType, templateName);
            return null;
        }
    }

    /// <summary>
    /// Returns a built-in default template for the given document type and template name.
    /// Word templates are keyed by name; other doc types return a generic placeholder.
    /// </summary>
    private static string GetDefaultTemplate(ReportingDocumentType docType, string templateName)
    {
        if (docType == ReportingDocumentType.Word)
        {
            return templateName switch
            {
                "ExecutiveSummary" => DefaultWordExecutiveSummaryTemplate,
                _ => DefaultWordExecutiveSummaryTemplate
            };
        }

        // Placeholder for non-Word types (Excel, PDF, Email) — out of POC scope
        return $"# {{{{Header}}}}\n {{{{foreach results from aspectkpi}}}}\n## {{{{Aspect Name}}}}\n   {{{{Aspect Result}}}}";
    }

    private const string DefaultWordExecutiveSummaryTemplate = """
        # {{ReportTitle}}

        **Generated**: {{GeneratedDate}}
        **Entity**: {{EntityName}}
        **Period**: {{ReportingPeriod}}

        ---

        ## Executive Summary

        {{ExecutiveSummary}}

        > {{KeyFinding}}

        ## Metrics Overview

        | Metric | Value | Unit | Trend | Previous |
        |---|---|---|---|---|
        {{MetricRows}}

        ## Detailed Analysis

        ### {{AnalysisSectionTitle}}

        {{DetailedAnalysis}}

        ### Root Cause

        {{RootCauseAnalysis}}

        ## Recommendations

        {{Recommendations}}

        ## Supporting Data

        {{SupportingData}}

        ---

        *Report generated by CXObserve AI on {{GeneratedDate}}*
        """;
}
