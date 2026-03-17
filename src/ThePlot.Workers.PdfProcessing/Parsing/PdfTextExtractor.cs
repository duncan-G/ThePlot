using MuPDF.NET;

namespace ThePlot.Workers.PdfProcessing.Parsing;

/// <summary>
/// Isolates MuPDF dependency. Extracts text lines with bounding boxes from a PDF
/// using the structured text API (blocks → lines → spans).
/// </summary>
public static class PdfTextExtractor
{
    /// <param name="pdfBytes">Raw PDF bytes (may be a chunk with renumbered pages).</param>
    /// <param name="startPage">The actual page number of the first page in the PDF.
    /// Chunks produced by MuPDF renumber pages starting from 0, so this offset
    /// maps them back to their position in the original document.</param>
    public static List<RawLine> ExtractLines(byte[] pdfBytes, int startPage = 1)
    {
        var doc = new Document(stream: pdfBytes, fileType: "pdf");
        try
        {
            var rawLines = new List<RawLine>();

            for (var pageIdx = 0; pageIdx < doc.PageCount; pageIdx++)
            {
                var page = doc.LoadPage(pageIdx);
                var actualPageNum = startPage + pageIdx;
                ExtractLinesFromPage(page, actualPageNum, rawLines);
            }

            rawLines.Sort(static (a, b) =>
            {
                var cmp = a.PageNum.CompareTo(b.PageNum);
                if (cmp != 0) return cmp;
                cmp = a.Bbox.Y0.CompareTo(b.Bbox.Y0);
                if (cmp != 0) return cmp;
                return a.Bbox.X0.CompareTo(b.Bbox.X0);
            });

            return rawLines;
        }
        finally
        {
            doc.Close();
        }
    }

    private static void ExtractLinesFromPage(Page page, int pageNum, List<RawLine> rawLines)
    {
        var textPage = page.GetTextPage(null, 0, null);
        var pageInfo = textPage.ExtractDict(null, true);

        foreach (var block in pageInfo.Blocks)
        {
            if (block.Type != 0)
                continue;

            foreach (var line in block.Lines)
            {
                var text = string.Concat(line.Spans.Select(s => s.Text)).Trim();
                if (text.Length == 0)
                    continue;

                var bbox = new BoundingBox(
                    line.Bbox.X0,
                    line.Bbox.Y0,
                    line.Bbox.X1,
                    line.Bbox.Y1);

                rawLines.Add(new RawLine(pageNum, text, bbox));
            }
        }
    }
}
