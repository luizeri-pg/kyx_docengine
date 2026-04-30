using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// O rodapé do Chromium (<c>FooterTemplate</c>) só cobre o PDF gerado a partir do HTML.
/// Os PDFs em <c>config.pdfsAnexos*</c> / <c>anexosPdf</c> são concatenados depois, sem esse rodapé.
/// Quando <c>DOCENGINE_USE_CHROME_PAGE_FOOTER</c> está activo e existe <c>HASH_DOSSIE</c>,
/// desenhamos hash + numeração global (página / total do PDF final) nas páginas dos anexos.
/// </summary>
public static class PdfDossieAnnexFooterStamper
{
    private const string ChromeFooterKey = "DOCENGINE_USE_CHROME_PAGE_FOOTER";
    private const string HashDossieKey = "HASH_DOSSIE";

    /// <summary>
    /// Conta páginas de um PDF (só leitura).
    /// </summary>
    public static int GetPdfPageCount(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
        {
            return 0;
        }

        try
        {
            using var ms = new MemoryStream(pdfBytes, writable: false);
            using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
            return doc.PageCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Se houver páginas de anexo e o modo rodapé Chrome + hash estiverem activos, carimba essas páginas.
    /// </summary>
    public static byte[] StampAnnexPagesIfApplicable(
        byte[] mergedPdf,
        int mainPdfPageCount,
        IReadOnlyDictionary<string, string> dados)
    {
        if (mergedPdf.Length == 0 || mainPdfPageCount <= 0)
        {
            return mergedPdf;
        }

        if (!dados.TryGetValue(ChromeFooterKey, out var footerFlag) || !IsTruthyChromeFooterFlag(footerFlag))
        {
            return mergedPdf;
        }

        if (!dados.TryGetValue(HashDossieKey, out var hashRaw) || string.IsNullOrWhiteSpace(hashRaw))
        {
            return mergedPdf;
        }

        var hash = hashRaw.Trim();
        int totalPages;
        try
        {
            using var probe = new MemoryStream(mergedPdf, writable: false);
            using var probeDoc = PdfReader.Open(probe, PdfDocumentOpenMode.Import);
            totalPages = probeDoc.PageCount;
        }
        catch
        {
            return mergedPdf;
        }

        if (mainPdfPageCount >= totalPages)
        {
            return mergedPdf;
        }

        if (!AnnexFooterPdfFonts.TryEnsureStampFont() || AnnexFooterPdfFonts.StampFamily is not { } stampFamily)
        {
            return mergedPdf;
        }

        try
        {
            using var workMs = new MemoryStream();
            workMs.Write(mergedPdf);
            workMs.Position = 0;
            using var doc = PdfReader.Open(workMs, PdfDocumentOpenMode.Modify);
            var font = new XFont(stampFamily, 7, XFontStyleEx.Regular);
            var fontPageNum = new XFont(stampFamily, 9, XFontStyleEx.Regular);
            var brushMuted = new XSolidBrush(XColor.FromArgb(68, 68, 68));
            var fmtLeft = new XStringFormat
            {
                Alignment = XStringAlignment.Near,
                LineAlignment = XLineAlignment.Far
            };
            var fmtRight = new XStringFormat
            {
                Alignment = XStringAlignment.Far,
                LineAlignment = XLineAlignment.Far
            };

            var hashDraw = hash.Length > 120 ? hash[..117] + "…" : hash;

            for (var i = mainPdfPageCount; i < doc.PageCount; i++)
            {
                var page = doc.Pages[i];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append, XPageDirection.Downwards);
                var w = page.Width.Point;
                var h = page.Height.Point;
                const double padX = 18;
                const double padBottom = 10;
                const double bandH = 22;
                var yTop = h - padBottom - bandH;
                var rectHash = new XRect(padX, yTop, w * 0.74 - padX, bandH);
                var rectNum = new XRect(w * 0.74, yTop, w * 0.26 - padX, bandH);
                var pageLabel = $"{i + 1} / {totalPages}";
                gfx.DrawString(hashDraw, font, brushMuted, rectHash, fmtLeft);
                gfx.DrawString(pageLabel, fontPageNum, XBrushes.Black, rectNum, fmtRight);
            }

            using var outMs = new MemoryStream();
            doc.Save(outMs, false);
            return outMs.ToArray();
        }
        catch
        {
            return mergedPdf;
        }
    }

    private static bool IsTruthyChromeFooterFlag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var v = value.Trim();
        return v.Equals("1", StringComparison.Ordinal)
               || v.Equals("true", StringComparison.OrdinalIgnoreCase)
               || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || v.Equals("sim", StringComparison.OrdinalIgnoreCase);
    }
}
