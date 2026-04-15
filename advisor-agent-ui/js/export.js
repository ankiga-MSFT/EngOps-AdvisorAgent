// ── Export Utilities ────────────────────────────────────
// CSV, Word, and PDF export for agent responses.

const ExportUtils = (() => {
    'use strict';

    /**
     * Extract a table element's data and trigger a CSV download.
     */
    function tableToCSV(table) {
        const rows = [];
        table.querySelectorAll('tr').forEach(tr => {
            const cells = [];
            tr.querySelectorAll('th, td').forEach(cell => {
                // Escape double quotes and wrap in quotes
                const text = cell.textContent.trim().replace(/"/g, '""');
                cells.push(`"${text}"`);
            });
            rows.push(cells.join(','));
        });
        return rows.join('\n');
    }

    function downloadCSV(table, filename) {
        const csv = tableToCSV(table);
        const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8;' });
        triggerDownload(blob, filename || 'table-export.csv');
    }

    /**
     * Export the answer content element as a Word document (.doc).
     * Uses an HTML blob with Word-compatible markup.
     */
    function exportToWord(contentEl, filename) {
        const html = `
      <html xmlns:o="urn:schemas-microsoft-com:office:office"
            xmlns:w="urn:schemas-microsoft-com:office:word"
            xmlns="http://www.w3.org/TR/REC-html40">
      <head><meta charset="utf-8">
        <style>
          body { font-family: Calibri, sans-serif; font-size: 11pt; color: #2d2d2d; line-height: 1.7; margin: 24pt; }
          h1 { font-size: 20pt; color: #1a3a6b; border-bottom: 2pt solid #2672d9; padding-bottom: 6pt; margin-top: 24pt; }
          h2 { font-size: 15pt; color: #2672d9; margin-top: 20pt; }
          h3 { font-size: 12pt; color: #16825d; font-style: italic; margin-top: 16pt; }
          h4 { font-size: 11pt; color: #7719aa; font-weight: bold; margin-top: 12pt; }
          p { margin: 6pt 0; }
          strong { color: #1a1a1a; }
          em { color: #555; }
          table { border-collapse: collapse; width: 100%; margin: 14pt 0; }
          th { background: #2672d9; color: #fff; font-weight: bold; padding: 8pt 12pt; text-align: left; font-size: 10pt; border: 1px solid #1a5bb5; }
          td { border: 1px solid #d1d1d1; padding: 7pt 12pt; text-align: left; font-size: 10pt; }
          tr:nth-child(even) td { background: #f4f7fb; }
          ul, ol { margin: 8pt 0; padding-left: 24pt; }
          li { margin-bottom: 4pt; line-height: 1.6; }
          code { font-family: Consolas, monospace; background: #f0f4f8; padding: 1pt 5pt; font-size: 10pt; color: #c4314b; border-radius: 3pt; }
          pre { background: #f8f8f8; padding: 10pt 14pt; font-size: 10pt; border: 1px solid #ddd; border-radius: 4pt; overflow-x: auto; }
          pre code { background: none; color: inherit; padding: 0; }
          hr { border: none; border-top: 1pt solid #e0e0e0; margin: 18pt 0; }
          blockquote { border-left: 3pt solid #2672d9; padding-left: 12pt; color: #555; font-style: italic; margin: 12pt 0; }
          a { color: #2672d9; text-decoration: underline; }
        </style>
      </head>
      <body>${cleanContentForExport(contentEl)}</body>
      </html>`;
        const blob = new Blob([html], { type: 'application/msword' });
        triggerDownload(blob, filename || 'advisor-response.doc');
    }

    /**
     * Export the answer content element as a PDF.
     * Uses html2pdf.js if available, falls back to window.print().
     */
    function exportToPDF(contentEl, filename) {
        const pdfStyles = `
          <style>
            body { font-family: 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 10pt; color: #2d2d2d; line-height: 1.7; word-wrap: break-word; overflow-wrap: break-word; }
            h1 { font-size: 18pt; color: #1a3a6b; border-bottom: 2px solid #2672d9; padding-bottom: 5px; margin-top: 20px; }
            h2 { font-size: 14pt; color: #2672d9; margin-top: 18px; }
            h3 { font-size: 11pt; color: #16825d; font-style: italic; margin-top: 14px; }
            h4 { font-size: 10pt; color: #7719aa; font-weight: bold; margin-top: 10px; }
            p { margin: 5px 0; }
            strong { color: #1a1a1a; }
            em { color: #555; }
            table { border-collapse: collapse; width: 100%; margin: 12px 0; table-layout: fixed; }
            th { background: #2672d9; color: #fff; font-weight: bold; padding: 6px 8px; text-align: left; font-size: 8pt; border: 1px solid #1a5bb5; word-break: break-word; overflow-wrap: break-word; }
            td { border: 1px solid #d1d1d1; padding: 5px 8px; text-align: left; font-size: 8pt; word-break: break-word; overflow-wrap: break-word; }
            tr:nth-child(even) td { background: #f4f7fb; }
            ul, ol { margin: 6px 0; padding-left: 22px; }
            li { margin-bottom: 3px; }
            code { font-family: Consolas, monospace; background: #f0f4f8; padding: 1px 4px; font-size: 8pt; color: #c4314b; border-radius: 2px; word-break: break-all; }
            pre { background: #f8f8f8; padding: 8px 12px; font-size: 8pt; border: 1px solid #ddd; border-radius: 3px; overflow-x: auto; white-space: pre-wrap; word-break: break-all; }
            pre code { background: none; color: inherit; padding: 0; }
            hr { border: none; border-top: 1px solid #e0e0e0; margin: 14px 0; }
            blockquote { border-left: 3px solid #2672d9; padding-left: 10px; color: #555; font-style: italic; margin: 10px 0; }
            a { color: #2672d9; }
          </style>`;

        if (typeof html2pdf !== 'undefined') {
            const wrapper = document.createElement('div');
            wrapper.innerHTML = pdfStyles + cleanContentForExport(contentEl);

            const opt = {
                margin: [10, 10, 10, 10],
                filename: filename || 'advisor-response.pdf',
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2, useCORS: true, scrollY: 0, width: 1120 },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'landscape' },
                pagebreak: { mode: ['avoid-all', 'css', 'legacy'] }
            };
            html2pdf().set(opt).from(wrapper).save();
        } else {
            // Fallback: open print dialog
            const win = window.open('', '_blank');
            win.document.write(`
        <html><head><title>Advisor Response</title>
        ${pdfStyles}</head>
        <body>${cleanContentForExport(contentEl)}</body></html>`);
            win.document.close();
            win.print();
        }
    }

    /**
     * Cleans the content HTML for export by removing UI-only elements.
     */
    function cleanContentForExport(contentEl) {
        const clone = contentEl.cloneNode(true);
        // Remove CSV buttons, followup chips, feedback buttons
        clone.querySelectorAll('.csv-download-btn, .followups, .feedback-row, .table-wrapper').forEach(el => {
            // For table-wrappers, unwrap the table
            if (el.classList.contains('table-wrapper')) {
                const table = el.querySelector('table');
                if (table) el.parentNode.insertBefore(table, el);
            }
            if (!el.classList.contains('table-wrapper')) return;
            el.remove();
        });
        // Remove remaining wrappers
        clone.querySelectorAll('.table-wrapper').forEach(w => {
            while (w.firstChild) w.parentNode.insertBefore(w.firstChild, w);
            w.remove();
        });
        clone.querySelectorAll('.csv-download-btn').forEach(el => el.remove());
        return clone.innerHTML;
    }

    function triggerDownload(blob, filename) {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }

    return { downloadCSV, exportToWord, exportToPDF };
})();
