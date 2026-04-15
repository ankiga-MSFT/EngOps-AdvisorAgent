using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace CXOAI.Tools;

/// <summary>
/// Converts LLM-produced markdown into a valid .docx byte array using OpenXml.
/// Supports headings (H1–H4), tables, bullets, numbered lists, blockquotes,
/// fenced code blocks, horizontal rules, and inline formatting (bold, italic, code, links).
/// </summary>
internal static class WordDocumentBuilder
{
    // ═══════════════════════════════════════════════════════════════
    // Regex patterns
    // ═══════════════════════════════════════════════════════════════

    private static readonly Regex NumberedListRegex = new(@"^(\d+)\.\s(.*)$", RegexOptions.Compiled);
    private static readonly Regex InlineFormattingRegex = new(
        @"(\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|\*(.+?)\*|_(.+?)_|`(.+?)`|\!\[([^\]]*)\]\(([^\)]+)\)|\[([^\]]*)\]\(([^\)]+)\))",
        RegexOptions.Compiled);

    // ═══════════════════════════════════════════════════════════════
    // Document styling constants
    // ═══════════════════════════════════════════════════════════════

    private static class Styles
    {
        internal const string MonospaceFont = "Courier New";
        internal const string CodeBlockFill = "F2F2F2";
        internal const string TableHeaderFill = "D9E2F3";
        internal const string TableBorderColor = "999999";
        internal const string BlockquoteBorderColor = "4472C4";
        internal const string HorizontalRuleColor = "AAAAAA";
        internal const string LinkTextColor = "4472C4";
        internal const string LinkUrlColor = "888888";
        internal const string MutedTextColor = "666666";
    }

    // ═══════════════════════════════════════════════════════════════
    // Main entry point
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses markdown text into a Word document and returns the .docx bytes.
    /// </summary>
    internal static byte[] Build(string markdown)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            var lines = markdown.Split('\n');
            var i = 0;
            var inCodeBlock = false;
            var codeBlockLines = new List<string>();
            var codeBlockLang = string.Empty;

            while (i < lines.Length)
            {
                var rawLine = lines[i];
                var trimmed = rawLine.TrimStart();

                // Fenced code block toggle
                if (trimmed.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        codeBlockLang = trimmed.Length > 3 ? trimmed[3..].Trim() : "";
                        codeBlockLines.Clear();
                    }
                    else
                    {
                        body.AppendChild(CreateCodeBlock(codeBlockLines, codeBlockLang));
                        inCodeBlock = false;
                        codeBlockLines.Clear();
                    }
                    i++;
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(rawLine);
                    i++;
                    continue;
                }

                // Headings (H1–H4, check longest prefix first)
                if (trimmed.StartsWith("#### "))
                    body.AppendChild(CreateHeading(trimmed[5..].Trim(), 4));
                else if (trimmed.StartsWith("### "))
                    body.AppendChild(CreateHeading(trimmed[4..].Trim(), 3));
                else if (trimmed.StartsWith("## "))
                    body.AppendChild(CreateHeading(trimmed[3..].Trim(), 2));
                else if (trimmed.StartsWith("# "))
                    body.AppendChild(CreateHeading(trimmed[2..].Trim(), 1));

                // Horizontal rule
                else if (trimmed.Length >= 3 && trimmed.Trim().All(c => c == '-'))
                    body.AppendChild(CreateHorizontalRule());

                // Blockquote
                else if (trimmed.StartsWith("> "))
                    body.AppendChild(CreateBlockquote(trimmed[2..].Trim()));

                // Nested bullet (2+ leading spaces then - or *)
                else if (rawLine.Length > 2 && rawLine.StartsWith("  ") &&
                         (rawLine.TrimStart().StartsWith("- ") || rawLine.TrimStart().StartsWith("* ")))
                    body.AppendChild(CreateNestedBullet(rawLine.TrimStart()[2..].Trim()));

                // Bullet list
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    body.AppendChild(CreateBullet(trimmed[2..].Trim()));

                // Numbered list
                else if (NumberedListRegex.Match(trimmed) is { Success: true } nlMatch)
                    body.AppendChild(CreateNumberedItem(nlMatch.Groups[1].Value, nlMatch.Groups[2].Value));

                // Table (pipe-delimited rows)
                else if (trimmed.StartsWith("| ") && !IsTableSeparator(trimmed))
                {
                    var tableLines = new List<string>();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                    {
                        var tl = lines[i].TrimStart();
                        if (!IsTableSeparator(tl))
                            tableLines.Add(tl);
                        i++;
                    }
                    if (tableLines.Count > 0)
                        body.AppendChild(CreateTable(tableLines));
                    continue; // i already advanced past table block
                }

                // Non-empty paragraph with inline formatting
                else if (!string.IsNullOrWhiteSpace(trimmed))
                    body.AppendChild(CreateParagraph(trimmed));

                i++;
            }

            // Flush unclosed code block
            if (inCodeBlock && codeBlockLines.Count > 0)
                body.AppendChild(CreateCodeBlock(codeBlockLines, codeBlockLang));
        }
        return ms.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════
    // Element creators
    // ═══════════════════════════════════════════════════════════════

    private static Paragraph CreateHeading(string text, int level)
    {
        var fontSize = level switch { 1 => "32", 2 => "28", 3 => "24", _ => "22" };
        var runProps = new RunProperties(new Bold(), new FontSize { Val = fontSize });
        if (level >= 4) runProps.AppendChild(new Italic());

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }),
            new Run(runProps, new Text(text)));
    }

    private static Paragraph CreateHorizontalRule()
    {
        return new Paragraph(new ParagraphProperties(
            new ParagraphBorders(
                new BottomBorder { Val = BorderValues.Single, Size = 6, Color = Styles.HorizontalRuleColor, Space = 1 }),
            new SpacingBetweenLines { Before = "200", After = "200" }));
    }

    private static Paragraph CreateBullet(string text)
    {
        var para = new Paragraph(new ParagraphProperties(
            new Indentation { Left = "720" },
            new SpacingBetweenLines { After = "60" }));
        foreach (var r in ParseInlineFormatting($"\u2022  {text}"))
            para.AppendChild(r);
        return para;
    }

    private static Paragraph CreateNestedBullet(string text)
    {
        var para = new Paragraph(new ParagraphProperties(
            new Indentation { Left = "1440" },
            new SpacingBetweenLines { After = "60" }));
        foreach (var r in ParseInlineFormatting($"\u25E6  {text}"))
            para.AppendChild(r);
        return para;
    }

    private static Paragraph CreateNumberedItem(string number, string text)
    {
        var para = new Paragraph(new ParagraphProperties(
            new Indentation { Left = "720" },
            new SpacingBetweenLines { After = "60" }));
        para.AppendChild(new Run(
            new RunProperties(new Bold()),
            new Text($"{number}. ") { Space = SpaceProcessingModeValues.Preserve }));
        foreach (var r in ParseInlineFormatting(text))
            para.AppendChild(r);
        return para;
    }

    private static Paragraph CreateBlockquote(string text)
    {
        var para = new Paragraph(new ParagraphProperties(
            new Indentation { Left = "720" },
            new ParagraphBorders(
                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = Styles.BlockquoteBorderColor, Space = 8 }),
            new SpacingBetweenLines { Before = "120", After = "120" }));

        foreach (var r in ParseInlineFormatting(text))
        {
            if (r is Run run)
            {
                run.RunProperties ??= new RunProperties();
                if (run.RunProperties.GetFirstChild<Italic>() == null)
                    run.RunProperties.AppendChild(new Italic());
            }
            para.AppendChild(r);
        }
        return para;
    }

    private static Paragraph CreateCodeBlock(List<string> lines, string language)
    {
        var para = new Paragraph(new ParagraphProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = Styles.CodeBlockFill },
            new Indentation { Left = "360", Right = "360" },
            new SpacingBetweenLines { Before = "120", After = "120" }));

        if (!string.IsNullOrWhiteSpace(language))
        {
            para.AppendChild(new Run(
                new RunProperties(
                    new RunFonts { Ascii = Styles.MonospaceFont, HighAnsi = Styles.MonospaceFont },
                    new FontSize { Val = "16" }, new Bold(), new Color { Val = Styles.MutedTextColor }),
                new Text($"[{language}]") { Space = SpaceProcessingModeValues.Preserve }));
            para.AppendChild(new Run(new Break()));
        }

        for (int j = 0; j < lines.Count; j++)
        {
            para.AppendChild(new Run(
                new RunProperties(
                    new RunFonts { Ascii = Styles.MonospaceFont, HighAnsi = Styles.MonospaceFont },
                    new FontSize { Val = "18" }),
                new Text(lines[j]) { Space = SpaceProcessingModeValues.Preserve }));
            if (j < lines.Count - 1)
                para.AppendChild(new Run(new Break()));
        }
        return para;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var para = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "100" }));
        foreach (var r in ParseInlineFormatting(text))
            para.AppendChild(r);
        return para;
    }

    private static Table CreateTable(List<string> rows)
    {
        // Determine column count from the first row for TableGrid and cell widths.
        var firstRowCells = rows[0]
            .Split('|', StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrEmpty(c))
            .ToArray();
        int colCount = firstRowCells.Length;

        var table = new Table();

        // TableProperties — borders, full width, and cell margins for web renderer compat.
        var tblProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Styles.TableBorderColor }),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableCellMarginDefault(
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new StartMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new EndMargin { Width = "80", Type = TableWidthUnitValues.Dxa }),
            new TableLook { Val = "04A0", FirstRow = true, LastRow = false, FirstColumn = true, LastColumn = false, NoHorizontalBand = false, NoVerticalBand = true });
        table.AppendChild(tblProps);

        // TableGrid — explicit column definitions required by Teams / Word Online.
        var tableGrid = new TableGrid();
        int colWidthDxa = 9000 / colCount; // distribute evenly across ~6.25 inch page width
        for (int c = 0; c < colCount; c++)
            tableGrid.AppendChild(new GridColumn { Width = colWidthDxa.ToString() });
        table.AppendChild(tableGrid);

        // Cell width string (percentage-based, evenly split).
        string cellWidthPct = (5000 / colCount).ToString();

        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var cells = rows[rowIdx]
                .Split('|', StringSplitOptions.TrimEntries)
                .Where(c => !string.IsNullOrEmpty(c))
                .ToArray();

            var tr = new TableRow();
            bool isHeader = rowIdx == 0;

            foreach (var cellText in cells)
            {
                var tc = new TableCell();
                var tcProps = new TableCellProperties(
                    new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = cellWidthPct },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

                if (isHeader)
                {
                    tcProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = Styles.TableHeaderFill });
                    tc.AppendChild(tcProps);
                    tc.AppendChild(new Paragraph(new Run(
                        new RunProperties(new Bold()),
                        new Text(cellText.Trim()))));
                }
                else
                {
                    tc.AppendChild(tcProps);
                    var cellPara = new Paragraph();
                    foreach (var r in ParseInlineFormatting(cellText.Trim()))
                        cellPara.AppendChild(r);
                    tc.AppendChild(cellPara);
                }
                tr.AppendChild(tc);
            }
            table.AppendChild(tr);
        }
        return table;
    }

    // ═══════════════════════════════════════════════════════════════
    // Inline formatting parser
    // Handles: ***bold+italic***, **bold**, *italic*, _italic_,
    //          `inline code`, ![image](url), [link](url)
    // ═══════════════════════════════════════════════════════════════

    private static List<OpenXmlElement> ParseInlineFormatting(string text)
    {
        var runs = new List<OpenXmlElement>();
        int lastIndex = 0;

        foreach (Match match in InlineFormattingRegex.Matches(text))
        {
            if (match.Index > lastIndex)
                AddPlainRun(runs, text[lastIndex..match.Index]);

            if (match.Groups[2].Success) // ***bold+italic***
                runs.Add(new Run(new RunProperties(new Bold(), new Italic()),
                    new Text(match.Groups[2].Value) { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[3].Success) // **bold**
                runs.Add(new Run(new RunProperties(new Bold()),
                    new Text(match.Groups[3].Value) { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[4].Success) // *italic*
                runs.Add(new Run(new RunProperties(new Italic()),
                    new Text(match.Groups[4].Value) { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[5].Success) // _italic_
                runs.Add(new Run(new RunProperties(new Italic()),
                    new Text(match.Groups[5].Value) { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[6].Success) // `inline code`
                runs.Add(new Run(new RunProperties(
                        new RunFonts { Ascii = Styles.MonospaceFont, HighAnsi = Styles.MonospaceFont },
                        new FontSize { Val = "18" },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = Styles.CodeBlockFill }),
                    new Text(match.Groups[6].Value) { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[7].Success) // ![alt](url)
                runs.Add(new Run(new RunProperties(new Italic(), new Color { Val = Styles.MutedTextColor }),
                    new Text($"[Image: {match.Groups[7].Value} \u2014 {match.Groups[8].Value}]") { Space = SpaceProcessingModeValues.Preserve }));
            else if (match.Groups[9].Success) // [text](url)
            {
                runs.Add(new Run(new RunProperties(new Bold(), new Color { Val = Styles.LinkTextColor }),
                    new Text(match.Groups[9].Value) { Space = SpaceProcessingModeValues.Preserve }));
                runs.Add(new Run(new RunProperties(new Color { Val = Styles.LinkUrlColor }, new FontSize { Val = "18" }),
                    new Text($" ({match.Groups[10].Value})") { Space = SpaceProcessingModeValues.Preserve }));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            AddPlainRun(runs, text[lastIndex..]);

        if (runs.Count == 0)
            runs.Add(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

        return runs;
    }

    private static bool IsTableSeparator(string line)
    {
        return string.IsNullOrWhiteSpace(line.Replace("|", "").Replace("-", "").Replace(":", "").Trim());
    }

    private static void AddPlainRun(List<OpenXmlElement> runs, string text)
    {
        if (!string.IsNullOrEmpty(text))
            runs.Add(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
