using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CXOAI.Tools;

// ═══════════════════════════════════════════════════════════════
// Input models for ReportingSkill tools
// ═══════════════════════════════════════════════════════════════

[Description("Input for GetReportingTemplatesAsync specifying the document type and template name.")]
public class ReportingTemplateInput
{
    [Description("The type of document to generate: Excel, Word, Pdf, or Email.")]
    public ReportingDocumentType ReportingDocumentType { get; set; }

    [Description("The name of the reporting template to retrieve (e.g. 'ExecutiveSummary'). Leave empty for the default template.")]
    public string ReportingTemplateName { get; set; } = string.Empty;
}

[Description("Input for GenerateWordAsync. Both fields are required.")]
public class WordToolInput
{
    [Description("The Word template string returned by GetReportingTemplatesAsync. Pass the exact string you received from that tool.")]
    [JsonPropertyName("wordTemplateFromGetReportingTemplatesAsync")]
    public string WordTemplateFromGetReportingTemplatesAsync { get; set; } = string.Empty;

    [Description("REQUIRED. The complete markdown text that YOU (the LLM) must compose using the user's data and the template structure. Write a full markdown document (headings, tables, bullet points, etc.) that fills in every section of the template. This must NOT be empty or null.")]
    [JsonPropertyName("llmProvidesMarkdownText")]
    public string LLMProvidesMarkdownText { get; set; } = string.Empty;
}

[Description("Input for GenerateExcelAsync.")]
public class ExcelToolInput
{
    [Description("The Excel template string returned by GetReportingTemplatesAsync.")]
    public string ExcelTemplate { get; set; } = string.Empty;

    [Description("The data to populate the Excel report with, typically markdown or structured text from upstream skills.")]
    public string TextData { get; set; } = string.Empty;
}

[Description("Input for GeneratePdfAsync.")]
public class PdfToolInput
{
    [Description("The PDF template string returned by GetReportingTemplatesAsync.")]
    public string PdfTemplate { get; set; } = string.Empty;

    [Description("The data to populate the PDF report with.")]
    public string TextData { get; set; } = string.Empty;
}

[Description("Input for SendEmailAsync.")]
public class EmailToolInput
{
    [Description("Recipient email address.")]
    public string To { get; set; } = string.Empty;

    [Description("CC email address (optional).")]
    public string cc { get; set; } = string.Empty;

    [Description("Email subject line.")]
    public string Subject { get; set; } = string.Empty;

    [Description("Email body content in markdown or plain text.")]
    public string Body { get; set; } = string.Empty;
}

public enum ReportingDocumentType
{
    Excel,
    Word,
    Pdf,
    Email
}
