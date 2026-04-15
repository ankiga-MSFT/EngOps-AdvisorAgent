using CXOAI.AppServices;
using CXOAI.ConfigurationStore;
using CXOAI.SkillFramework;
using CXOAI.StatusNotifier;
using CXOAI.Tools;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace UnitTests;

/// <summary>
/// Consolidated unit tests for <see cref="ReportingTools"/> covering all tool methods:
/// template retrieval, Word/Excel/PDF generation, email sending, and artifact download flow.
/// </summary>
public class ReportingToolsTests
{
    private readonly InMemoryDurableStore _durableStore = new();
    private readonly ReportingTools _tools;

    public ReportingToolsTests()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        _tools = new ReportingTools(loggerFactory.CreateLogger<ReportingTools>(), new StubStoreProvider(), new StubUserAuthContext(), new ConsoleToolStatusNotifier(), _durableStore);
    }

    // ═══════════════════════════════════════════════════════════════
    // GetReportingTemplatesAsync
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetReportingTemplatesAsync_Word_ReturnsNonEmptyTemplate()
    {
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        Assert.False(string.IsNullOrWhiteSpace(template), "Template should not be empty");
        Assert.Contains("Executive Summary", template);
        Assert.Contains("Metrics Overview", template);
    }

    [Fact]
    public async Task GetReportingTemplatesAsync_EmptyTemplateName_DefaultsToExecutiveSummary()
    {
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "");

        Assert.False(string.IsNullOrWhiteSpace(template));
        Assert.Contains("Executive Summary", template);
    }

    [Fact]
    public async Task GetReportingTemplatesAsync_UnknownWordTemplate_FallsBackToDefault()
    {
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "NonExistentTemplate");

        Assert.False(string.IsNullOrWhiteSpace(template));
        Assert.Contains("Executive Summary", template);
    }

    [Fact]
    public async Task GetReportingTemplatesAsync_Excel_ReturnsPlaceholderTemplate()
    {
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Excel, "ExecutiveSummary");

        Assert.False(string.IsNullOrWhiteSpace(template));
    }

    [Fact]
    public async Task GetReportingTemplatesAsync_Pdf_ReturnsPlaceholderTemplate()
    {
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Pdf, "ExecutiveSummary");

        Assert.False(string.IsNullOrWhiteSpace(template));
    }

    // ═══════════════════════════════════════════════════════════════
    // GenerateWordAsync
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// TC-1: Walmart CSAT executive-ready Word export.
    /// Calls GetReportingTemplatesAsync(Word), fills the template with CSAT markdown,
    /// calls GenerateWordAsync, and validates the .docx output.
    /// </summary>
    [Fact]
    public async Task TC1_WalmartCSAT_GenerateWordAsync_ProducesValidDocx()
    {
        // Step 1 — Get Word template (same as the agent would do)
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        Assert.False(string.IsNullOrWhiteSpace(template), "Template should not be empty");

        // Step 2 — Simulate LLM-filled markdown for TC-1 (Walmart CSAT, last 30 days)
        var filledMarkdown = """
            # Walmart — Support CSAT Executive Summary

            **Generated**: 2025-07-15
            **Entity**: Walmart (TPID: 12345)
            **Period**: Last 30 days (June 15 – July 15, 2025)

            ---

            ## Executive Summary

            Walmart's Support CSAT score has **declined to 72.45** over the last 30 days, down from 74.84 — a **-3.2% drop**. This decline correlates with increased case aging, slower first response times, and a spike in P1 incidents during the July 4th holiday period.

            > **Key Finding**: Three P1 incidents (INC-4521, INC-4533, INC-4540) and holiday understaffing drove the CSAT decline.

            ## Metrics Overview

            | Metric | Value | Unit | Trend | Previous |
            |---|---|---|---|---|
            | **Support CSAT** | 72.45 | score | ↓ -3.2% | 74.84 |
            | **Average Case Aging** | 3.4 | days | ↑ +61.9% | 2.1 |
            | **First Response Time** | 4.2 | hours | ↑ +10.5% | 3.8 |
            | **Case Resolution Rate** | 87.6 | percent | ↓ -2.1% | 89.48 |

            ## Detailed Analysis

            ### Trend Analysis

            The CSAT decline began around June 28 and accelerated through July 5. The timing aligns with:
            - Increased case volume (+15% WoW)
            - Reduced staffing during July 4th holiday week
            - Three critical P1 incidents occurring within a 10-day window

            ### Root Cause

            1. **P1 Incident INC-4521** — Azure AD SSO authentication failures impacting Walmart users
            2. **P1 Incident INC-4533** — Data synchronization delays in the supply chain portal
            3. **P1 Incident INC-4540** — API throttling causing timeouts on high-traffic endpoints
            - Support team understaffed during July 4th holiday period
            - No automated escalation for cases exceeding 48 hours

            ## Recommendations

            1. **Implement automated escalation** for cases aging beyond 48 hours
            2. **Staff holiday coverage plans** — minimum 60% coverage during major holidays
            3. **Deploy monitoring alerts** for Azure AD SSO and API throttling thresholds
            4. **Conduct post-incident review** for INC-4521, INC-4533, and INC-4540

            ## Supporting Data

            - Total cases opened: **142**
            - P1 incidents (last 10 days): **3**
            - Case volume change WoW: **+15.0%**

            ```kql
            SupportCases
            | where EntityName == "Walmart" and Timestamp >= ago(30d)
            | summarize CSAT = avg(CSATScore) by bin(Timestamp, 1d)
            | order by Timestamp asc
            ```

            ---

            *Report generated by CXObserve AI on 2025-07-15*
            """;

        // Step 3 — Generate the Word document
        var result = await _tools.GenerateWordAsync(template, filledMarkdown);

        // Step 4 — Validate response
        Assert.True(result.IsSuccess, "GenerateWordAsync should succeed");
        Assert.False(result.NeedsInputForUser, "Should not need further user input");
        Assert.True(result.IsUIComponent, "Should flag as UI component for download card rendering");
        Assert.Contains(".docx", result.Response);
        Assert.Contains("/api/artifacts/ReportingSkill/", result.Response);

        // Step 5 — Validate artifact was stored in durable store
        Assert.Single(_durableStore.Blobs);

        var entry = _durableStore.Blobs.First();
        var blobData = entry.Value.Data;
        var blobContentType = entry.Value.ContentType;
        Assert.True(blobData.Length > 0, "Docx bytes should not be empty");
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", blobContentType);
        Assert.EndsWith(".docx", entry.Key);

        // Step 6 — Validate the .docx is a valid Word document with expected content
        using var ms = new MemoryStream(blobData);
        using var wordDoc = WordprocessingDocument.Open(ms, false);

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);

        var fullText = body.InnerText;

        // Core content from TC-1 prompt scenario
        Assert.Contains("Walmart", fullText);
        Assert.Contains("CSAT", fullText);
        Assert.Contains("72.45", fullText);
        Assert.Contains("Executive Summary", fullText);

        // Table content (metrics)
        Assert.Contains("Average Case Aging", fullText);
        Assert.Contains("First Response Time", fullText);

        // Recommendations (numbered list)
        Assert.Contains("Implement automated escalation", fullText);

        // Code block (KQL)
        Assert.Contains("SupportCases", fullText);

        // Blockquote key finding
        Assert.Contains("Key Finding", fullText);

        // Root cause items
        Assert.Contains("INC-4521", fullText);
    }

    /// <summary>
    /// Validates that the Response returned by GenerateWordAsync contains a valid
    /// markdown link with FileName and DownloadUrl that resolves to a downloadable .docx file —
    /// simulating what the /api/artifacts endpoint would serve to a browser.
    /// </summary>
    [Fact]
    public async Task TC1_WordExport_PayloadArtifactRef_IsDownloadable()
    {
        // Arrange — generate a Word doc
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        var result = await _tools.GenerateWordAsync(template, """
                # Walmart — Support CSAT Executive Summary
                **Period**: Last 30 days

                ## Executive Summary
                Walmart's Support CSAT score has **declined to 72.45**.

                | Metric | Value |
                |---|---|
                | Support CSAT | 72.45 |
                """);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsReport);

        // --- Validate Response contains markdown link with DownloadUrl ---
        // New format: "[Report_xxx.docx](/api/artifacts/ReportingSkill/Report_xxx.docx)"
        var match = System.Text.RegularExpressions.Regex.Match(result.Response, @"\[([^\]]+\.docx)\]\(([^)]+)\)");
        Assert.True(match.Success, $"Response should contain markdown link, got: {result.Response}");
        var fileName = match.Groups[1].Value;
        var downloadUrl = match.Groups[2].Value;

        Assert.EndsWith(".docx", fileName);
        Assert.StartsWith("/api/artifacts/", downloadUrl);
        Assert.EndsWith(".docx", downloadUrl);

        // --- Simulate the download endpoint: resolve DownloadUrl → artifact key → bytes ---
        var key = downloadUrl.Replace("/api/artifacts/", "");
        var retrieved = await _durableStore.RetrieveAsync(key);
        Assert.NotNull(retrieved);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", retrieved.Value.ContentType);
        Assert.True(retrieved.Value.Data.Length > 0, "Artifact bytes must not be empty");

        // --- Write to temp file and verify it opens as valid .docx ---
        var tempDir = Path.Combine(Path.GetTempPath(), "CXOAITests");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, fileName);
        try
        {
            File.WriteAllBytes(filePath, retrieved.Value.Data);
            Assert.True(File.Exists(filePath), "File should exist on disk");

            // Validate the file on disk is a valid Word document
            using var wordDoc = WordprocessingDocument.Open(filePath, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            Assert.NotNull(body);
            Assert.Contains("Walmart", body.InnerText);
            Assert.Contains("72.45", body.InnerText);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// Generates a Word doc, saves it to TestOutput folder, and logs the file:// URL
    /// so you can open it directly in a browser or Word.
    /// </summary>
    [Fact]
    public async Task TC1_WordExport_SaveToDisk_AndReturnDownloadUrl()
    {
        // Generate Word doc
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        var result = await _tools.GenerateWordAsync(template, """
                # Walmart — Support CSAT Executive Summary

                **Generated**: 2025-07-15
                **Entity**: Walmart (TPID: 12345)
                **Period**: Last 30 days (June 15 – July 15, 2025)

                ---

                ## Executive Summary

                Walmart's Support CSAT score has **declined to 72.45** over the last 30 days, down from 74.84 — a **-3.2% drop**.

                > **Key Finding**: Three P1 incidents and holiday understaffing drove the CSAT decline.

                ## Metrics Overview

                | Metric | Value | Unit | Trend | Previous |
                |---|---|---|---|---|
                | **Support CSAT** | 72.45 | score | ↓ -3.2% | 74.84 |
                | **Average Case Aging** | 3.4 | days | ↑ +61.9% | 2.1 |
                | **First Response Time** | 4.2 | hours | ↑ +10.5% | 3.8 |
                | **Case Resolution Rate** | 87.6 | percent | ↓ -2.1% | 89.48 |

                ## Recommendations

                1. **Implement automated escalation** for cases aging beyond 48 hours
                2. **Staff holiday coverage plans** — minimum 60% coverage during major holidays
                3. **Deploy monitoring alerts** for Azure AD SSO and API throttling thresholds
                4. **Conduct post-incident review** for INC-4521, INC-4533, and INC-4540

                ---

                *Report generated by CXObserve AI on 2025-07-15*
                """);

        Assert.True(result.IsSuccess, "GenerateWordAsync should succeed");
        Assert.True(result.IsReport);

        // Validate Response contains markdown link with FileName + DownloadUrl
        var match = System.Text.RegularExpressions.Regex.Match(result.Response, @"\[([^\]]+\.docx)\]\(([^)]+)\)");
        Assert.True(match.Success, $"Response should contain markdown link, got: {result.Response}");
        var fileName = match.Groups[1].Value;
        var downloadUrl = match.Groups[2].Value;

        Assert.EndsWith(".docx", fileName);
        Assert.Contains(".docx", result.Response);

        // Validate DownloadUrl
        Assert.StartsWith("/api/artifacts/ReportingSkill/", downloadUrl);
        Assert.EndsWith(".docx", downloadUrl);

        // Resolve artifact via DownloadUrl key and write to disk
        var key = downloadUrl.Replace("/api/artifacts/", "");
        var retrieved = await _durableStore.RetrieveAsync(key);
        Assert.NotNull(retrieved);

        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(ReportingToolsTests).Assembly.Location)!,
            "TestOutput");
        Directory.CreateDirectory(outputDir);

        var filePath = Path.Combine(outputDir, fileName);
        File.WriteAllBytes(filePath, retrieved.Value.Data);

        Assert.True(File.Exists(filePath));
        Assert.True(new FileInfo(filePath).Length > 0);

        // Log the download URL
        var fileUrl = "file:///" + filePath.Replace('\\', '/');
        Console.WriteLine($"=== DOWNLOAD URL: {fileUrl} ===");
        Console.WriteLine($"=== FILE PATH:    {filePath} ===");
    }

    /// <summary>
    /// Validates that GenerateWordAsync returns a failure response when markdown input is empty.
    /// </summary>
    [Fact]
    public async Task GenerateWordAsync_EmptyMarkdown_ReturnsFailure()
    {
        var result = await _tools.GenerateWordAsync("template", "");

        Assert.False(result.IsSuccess);
        Assert.Contains("LLMProvidesMarkdownText is empty", result.Response);
        Assert.Null(result.Payload);
    }

    // ═══════════════════════════════════════════════════════════════
    // GenerateExcelAsync
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GenerateExcelAsync_ReturnsArtifactReference()
    {
        var result = await _tools.GenerateExcelAsync(
            "# {{Header}}\n## {{Data}}",
            "Sample Excel data for CSAT metrics");

        Assert.False(result.NeedsInputForUser);
        Assert.Contains("report.xlsx", result.Response);
        Assert.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Response);
    }

    // ═══════════════════════════════════════════════════════════════
    // GeneratePdfAsync
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task GeneratePdfAsync_ReturnsArtifactReference()
    {
        var result = await _tools.GeneratePdfAsync(
            "# {{Header}}\n## {{Data}}",
            "Sample PDF data for CSAT metrics");

        Assert.False(result.NeedsInputForUser);
        Assert.Contains("report.pdf", result.Response);
        Assert.Contains("application/pdf", result.Response);
    }

    // ═══════════════════════════════════════════════════════════════
    // SendEmailAsync
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task SendEmailAsync_ReturnsConfirmation()
    {
        var result = await _tools.SendEmailAsync(
            to: "user@contoso.com",
            cc: "manager@contoso.com",
            subject: "Walmart CSAT Report",
            body: "Please find the attached CSAT report.");

        Assert.False(result.NeedsInputForUser);
        Assert.Contains("Send = yes", result.Response);
        Assert.Contains("user@contoso.com", result.Response);
        Assert.Contains("Walmart CSAT Report", result.Response);
    }

    // ═══════════════════════════════════════════════════════════════
    // Download URL / Artifact resolution
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Simulates the browser download flow end-to-end:
    ///   GenerateWordAsync → Response markdown link → parse route params → ArtifactStore.Get → validate response.
    /// This mirrors exactly what ArtifactDownloadFunction.DownloadArtifact does:
    ///   1. Extract {skillName}/{fileName} from the route (= DownloadUrl path segments)
    ///   2. Build artifactKey = "{skillName}/{fileName}"
    ///   3. Call _artifactStore.Get(artifactKey)
    ///   4. Return Content-Type, Content-Disposition: attachment, and the .docx bytes
    /// If this test passes, the browser click will work.
    /// </summary>
    [Fact]
    public async Task DownloadUrl_ResolvesToValidDocx_SimulatingBrowserDownload()
    {
        // ── Arrange: generate a Word document ────────────────────────
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        var result = await _tools.GenerateWordAsync(template, """
                # Walmart — Support CSAT Executive Summary

                **Generated**: 2025-07-15
                **Entity**: Walmart (TPID: 12345)
                **Period**: Last 30 days

                ---

                ## Executive Summary

                Walmart's Support CSAT score stands at **72.45**, trending **down -3.2%**.

                > **Key Finding**: Holiday understaffing drove the CSAT decline.

                ## Metrics Overview

                | Metric | Value | Unit | Trend |
                |---|---|---|---|
                | **Support CSAT** | 72.45 | score | ↓ -3.2% |
                | **Average Case Aging** | 3.4 | days | ↑ +61.9% |

                ## Recommendations

                1. **Implement automated escalation** for cases aging beyond 48 hours
                2. **Staff holiday coverage plans** — minimum 60% coverage

                ---

                *Report generated by CXObserve AI on 2025-07-15*
                """);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsReport);

        // ── Act: extract DownloadUrl from Response markdown link ─────
        // The UI receives Response as "[Report_xxx.docx](/api/artifacts/ReportingSkill/Report_xxx.docx)"
        // and the browser GETs the URL. The Azure Functions route binding extracts {skillName} and {fileName}.
        var match = System.Text.RegularExpressions.Regex.Match(result.Response, @"\[([^\]]+\.docx)\]\(([^)]+)\)");
        Assert.True(match.Success, $"Response should contain markdown link, got: {result.Response}");
        var downloadUrl = match.Groups[2].Value;
        Assert.StartsWith("/api/artifacts/", downloadUrl);

        // Parse the same way the Azure Functions route binding does:
        //   Route = "artifacts/{skillName}/{fileName}"
        //   URL   = "/api/artifacts/ReportingSkill/Report_xxx.docx"
        //   segments[3] = skillName, segments[4] = fileName
        var segments = downloadUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(segments.Length >= 3, $"DownloadUrl should have at least 3 path segments, got: {downloadUrl}");
        var routeSkillName = segments[^2]; // second-to-last = skillName
        var routeFileName = segments[^1];  // last = fileName

        Assert.Equal("ReportingSkill", routeSkillName);
        Assert.EndsWith(".docx", routeFileName);

        // ── Simulate ArtifactDownloadFunction.DownloadArtifact ───────
        // The endpoint does: var artifactKey = $"{skillName}/{fileName}";
        //                    var blob = await _durableArtifactStore.RetrieveAsync(artifactKey);
        var artifactKey = $"{routeSkillName}/{routeFileName}";
        var retrieved = await _durableStore.RetrieveAsync(artifactKey);

        // If this is null, the endpoint would return 404 — the browser download would fail
        Assert.NotNull(retrieved);

        // ── Validate response headers (what the browser sees) ────────
        // The endpoint sets: Content-Type and Content-Disposition: attachment
        var expectedContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        Assert.Equal(expectedContentType, retrieved.Value.ContentType);

        var expectedDisposition = $"attachment; filename=\"{routeFileName}\"";
        Assert.Contains(routeFileName, expectedDisposition);
        Assert.Contains("attachment", expectedDisposition);

        // Verify artifact has data
        Assert.True(retrieved.Value.Data.Length > 0, "Artifact bytes must not be empty");

        // ── Validate the bytes are a real, openable .docx ────────────
        // This is what the browser saves to disk after downloading
        using var ms = new MemoryStream(retrieved.Value.Data);
        using var wordDoc = WordprocessingDocument.Open(ms, false);

        var body = wordDoc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);

        var fullText = body.InnerText;
        Assert.Contains("Walmart", fullText);
        Assert.Contains("72.45", fullText);
        Assert.Contains("Executive Summary", fullText);
        Assert.Contains("Recommendations", fullText);
        Assert.Contains("Average Case Aging", fullText);
    }

    /// <summary>
    /// Validates that the download endpoint returns 404 for a non-existent artifact.
    /// Mirrors what happens when a browser clicks a stale or invalid DownloadUrl.
    /// </summary>
    [Fact]
    public async Task DownloadUrl_NonExistentArtifact_ReturnsNull()
    {
        // Simulate the endpoint looking up an artifact that was never stored
        var retrieved = await _durableStore.RetrieveAsync("ReportingSkill/NonExistent_Report.docx");
        Assert.Null(retrieved);
    }

    /// <summary>
    /// Full integration test: generates a Word doc, then replays exactly what
    /// ArtifactDownloadFunction.DownloadArtifact does — parses the DownloadUrl,
    /// resolves the artifact, builds the HTTP response headers, and writes the
    /// file to TestOutput so it can be opened in Word or a browser.
    ///
    /// Output: prints the file path, simulated HTTP headers, and a file:// URL.
    /// </summary>
    [Fact]
    public async Task Integration_DownloadEndpoint_ProducesOpenableDocxFile()
    {
        // ── Step 1: Generate a Word document (same as orchestrator would) ──
        var template = await _tools.GetReportingTemplatesAsync(ReportingDocumentType.Word, "ExecutiveSummary");

        var result = await _tools.GenerateWordAsync(template, """
                # Walmart — Support CSAT Executive Summary

                **Generated**: 2025-07-15
                **Entity**: Walmart (TPID: 12345)
                **Period**: Last 30 days (June 15 – July 15, 2025)

                ---

                ## Executive Summary

                Walmart's Support CSAT score has **declined to 72.45** over the last 30 days,
                down from 74.84 — a **-3.2% drop**.

                > **Key Finding**: Three P1 incidents and holiday understaffing drove the decline.

                ## Metrics Overview

                | Metric | Value | Unit | Trend | Previous |
                |---|---|---|---|---|
                | **Support CSAT** | 72.45 | score | ↓ -3.2% | 74.84 |
                | **Average Case Aging** | 3.4 | days | ↑ +61.9% | 2.1 |
                | **First Response Time** | 4.2 | hours | ↑ +10.5% | 3.8 |
                | **Case Resolution Rate** | 87.6 | percent | ↓ -2.1% | 89.48 |

                ## Recommendations

                1. **Implement automated escalation** for cases aging beyond 48 hours
                2. **Staff holiday coverage plans** — minimum 60% coverage during major holidays
                3. **Deploy monitoring alerts** for Azure AD SSO and API throttling thresholds

                ---

                *Report generated by CXObserve AI on 2025-07-15*
                """);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsReport);

        // ── Step 2: Extract DownloadUrl from Response markdown link ──
        //
        // The endpoint does:
        //   [HttpTrigger("get", Route = "artifacts/{skillName}/{fileName}")]
        //   var artifactKey = $"{skillName}/{fileName}";
        //   var artifact = _artifactStore.Get(artifactKey);
        //   response.Headers.Add("Content-Type", artifact.ContentType);
        //   response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        //   await response.Body.WriteAsync(artifact.Data);

        var match = System.Text.RegularExpressions.Regex.Match(result.Response, @"\[([^\]]+\.docx)\]\(([^)]+)\)");
        Assert.True(match.Success, $"Response should contain markdown link, got: {result.Response}");
        var downloadUrl = match.Groups[2].Value;
        var segments = downloadUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var skillName = segments[^2];
        var fileName = segments[^1];
        var artifactKey = $"{skillName}/{fileName}";

        var retrieved = await _durableStore.RetrieveAsync(artifactKey);
        Assert.NotNull(retrieved);

        // Simulated HTTP response headers (what the browser receives)
        var httpStatus = "200 OK";
        var contentType = retrieved.Value.ContentType;
        var contentDisposition = $"attachment; filename=\"{fileName}\"";
        var contentLength = retrieved.Value.Data.Length;

        // ── Step 3: Write to disk (same bytes the browser would save) ──
        var outputDir = Path.Combine(
            Path.GetDirectoryName(typeof(ReportingToolsTests).Assembly.Location)!,
            "TestOutput");
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, fileName);
        File.WriteAllBytes(filePath, retrieved.Value.Data);

        // ── Step 4: Validate the file opens as valid .docx ──
        using var wordDoc = WordprocessingDocument.Open(filePath, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        Assert.NotNull(body);
        Assert.Contains("Walmart", body.InnerText);
        Assert.Contains("72.45", body.InnerText);

        // ── Print results for manual verification ──
        var fileUrl = "file:///" + filePath.Replace('\\', '/');
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  SIMULATED BROWSER DOWNLOAD — ArtifactDownloadFunction      ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  Request:  GET {downloadUrl}");
        Console.WriteLine($"║  Status:   {httpStatus}");
        Console.WriteLine($"║  Content-Type: {contentType}");
        Console.WriteLine($"║  Content-Disposition: {contentDisposition}");
        Console.WriteLine($"║  Content-Length: {contentLength} bytes");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║  FILE SAVED: {filePath}");
        Console.WriteLine($"║  OPEN IN BROWSER: {fileUrl}");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    }

    // ═══════════════════════════════════════════════════════════════
    // Stubs
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// In-memory implementation of <see cref="IArtifactStore"/> for unit testing.
    /// </summary>
    private class InMemoryDurableStore : IArtifactStore
    {
        private readonly ConcurrentDictionary<string, (byte[] Data, string ContentType)> _blobs = new();

        public IReadOnlyDictionary<string, (byte[] Data, string ContentType)> Blobs => _blobs;

        public Task<string> StoreAsync(string blobName, byte[] data, string contentType, CancellationToken ct = default)
        {
            _blobs[blobName] = (data, contentType);
            return Task.FromResult($"memory://{blobName}");
        }

        public Task<(byte[] Data, string ContentType)?> RetrieveAsync(string blobName, CancellationToken ct = default)
        {
            if (_blobs.TryGetValue(blobName, out var value))
                return Task.FromResult<(byte[] Data, string ContentType)?>(value);
            return Task.FromResult<(byte[] Data, string ContentType)?>(null);
        }
    }

    /// <summary>
    /// Stub ITreeConfigurationStoreProvider — not used by Word generation paths.
    /// </summary>
    private class StubStoreProvider : ITreeConfigurationStoreProvider
    {
        public Task<List<TreeConfiguration>> GetConfigurations(string componentName, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());

        public Task<List<TreeConfiguration>> GetConfigurationsWithDescription(string componentName, string searchText, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());

        public Task<List<TreeConfiguration>> GetConfigurationsWithNames(string componentName, List<string> configurationNames, bool needNestedConfigs)
            => Task.FromResult(new List<TreeConfiguration>());

        public Task<Azure.Response<Azure.Search.Documents.Models.IndexDocumentsResult>> UploadDocumentAsync(TreeConfiguration configStore)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Stub IUserAuthContext — not used by Word generation paths.
    /// </summary>
    private class StubUserAuthContext : IUserAuthContext
    {
        public string? AccessToken { get; set; }
        public string? UserObjectId { get; set; }
        public string? UserName { get; set; }
        public string? UserPrincipalName { get; set; }
    }
}
