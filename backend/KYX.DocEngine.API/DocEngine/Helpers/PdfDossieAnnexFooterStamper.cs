using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// O rodapé do Chromium (<c>FooterTemplate</c>) só cobre o PDF gerado a partir do HTML do template principal.
/// Os PDFs nativos concatenados a seguir — via <c>config.pdfsAnexos*</c> ou via <c>dados.anexosPdf[]</c> no payload
/// estruturado do dossiê — não passam pelo Chromium; a origem (HTML pré-renderizado, scanner, outro sistema) é irrelevante.
/// Quando <c>DOCENGINE_USE_CHROME_PAGE_FOOTER</c> está activo e existe <c>HASH_DOSSIE</c> (ou alias normalizado
/// <c>hashDossie</c> em <see cref="DossieEstruturadaMapper.NormalizeBrandingForGenerate"/>),
/// desenhamos hash + numeração global (página / total do PDF final) no rodapé de <strong>cada página só dos anexos</strong>.
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
        IReadOnlyDictionary<string, string> dados,
        ILogger? logger = null)
    {
        if (mergedPdf.Length == 0 || mainPdfPageCount <= 0)
        {
            logger?.LogDebug(
                "AnnexStamp: ignorado (mergedPdfBytes={Bytes}, mainPdfPageCount={Main}).",
                mergedPdf.Length,
                mainPdfPageCount);
            return mergedPdf;
        }

        if (!TryGetChromeFooterFlag(dados, out var footerFlag) || !IsTruthyChromeFooterFlag(footerFlag))
        {
            logger?.LogInformation(
                "AnnexStamp: ignorado — DOCENGINE_USE_CHROME_PAGE_FOOTER ausente ou false (valor={Footer}). Anexos ficam sem hash/numeração.",
                footerFlag ?? "<null>");
            return mergedPdf;
        }

        if (!TryGetHashDossie(dados, out var hashRaw))
        {
            logger?.LogWarning(
                "AnnexStamp: ignorado — HASH_DOSSIE / hashDossie ausente nos dados. Anexos ficam sem hash/numeração.");
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
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "AnnexStamp: ignorado — falha a contar páginas do PDF mergido.");
            return mergedPdf;
        }

        if (mainPdfPageCount >= totalPages)
        {
            logger?.LogDebug(
                "AnnexStamp: nada a carimbar (mainPdfPageCount={Main} >= totalPages={Total}).",
                mainPdfPageCount,
                totalPages);
            return mergedPdf;
        }

        if (!AnnexFooterPdfFonts.TryEnsureStampFont() || AnnexFooterPdfFonts.StampFamily is not { } stampFamily)
        {
            logger?.LogWarning(
                "AnnexStamp: ignorado — não foi possível resolver fonte para PdfSharp (AnnexFooterPdfFonts).");
            return mergedPdf;
        }

        var annexCount = totalPages - mainPdfPageCount;

        try
        {
            using var workMs = new MemoryStream();
            workMs.Write(mergedPdf);
            workMs.Position = 0;
            using var doc = PdfReader.Open(workMs, PdfDocumentOpenMode.Modify);
            DrawStampOnAnnexPages(doc, mainPdfPageCount, totalPages, hash, stampFamily);
            using var outMs = new MemoryStream();
            doc.Save(outMs, false);
            logger?.LogInformation(
                "AnnexStamp: carimbadas {AnnexCount} páginas de anexo (modo Modify), main={Main}, total={Total}.",
                annexCount,
                mainPdfPageCount,
                totalPages);
            return outMs.ToArray();
        }
        catch (Exception modifyEx)
        {
            logger?.LogWarning(
                modifyEx,
                "AnnexStamp: PdfReader.Open(Modify) falhou; a tentar fallback Import + reconstrução.");
        }

        try
        {
            using var inMs = new MemoryStream(mergedPdf, writable: false);
            using var src = PdfReader.Open(inMs, PdfDocumentOpenMode.Import);
            using var rebuilt = new PdfDocument();
            for (var i = 0; i < src.PageCount; i++)
            {
                rebuilt.AddPage(src.Pages[i]);
            }

            DrawStampOnAnnexPages(rebuilt, mainPdfPageCount, totalPages, hash, stampFamily);
            using var outMs = new MemoryStream();
            rebuilt.Save(outMs, false);
            logger?.LogInformation(
                "AnnexStamp: carimbadas {AnnexCount} páginas de anexo (fallback Import), main={Main}, total={Total}.",
                annexCount,
                mainPdfPageCount,
                totalPages);
            return outMs.ToArray();
        }
        catch (Exception fallbackEx)
        {
            logger?.LogError(
                fallbackEx,
                "AnnexStamp: fallback também falhou; PDF devolvido sem carimbo nas páginas {Main}-{Total}.",
                mainPdfPageCount + 1,
                totalPages);
            return mergedPdf;
        }
    }

    private static void DrawStampOnAnnexPages(
        PdfDocument doc,
        int mainPdfPageCount,
        int totalPages,
        string hash,
        string stampFamily)
    {
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
            const double padBottom = 6;
            const double bandH = 28;
            const double maskExtra = 14;
            var yTop = h - padBottom - bandH;
            /**
             * Anexos vêm de outros geradores (Chromium, scanner, etc) e podem trazer rodapé próprio
             * a poucos pontos do bottom. Pintamos uma faixa branca 100% opaca de borda a borda
             * que tapa o rodapé original antes de carimbar hash + numeração global.
             */
            var maskTop = Math.Max(0, yTop - maskExtra);
            var maskHeight = h - maskTop;
            gfx.DrawRectangle(XBrushes.White, new XRect(0, maskTop, w, maskHeight));
            var rectHash = new XRect(padX, yTop, w * 0.74 - padX, bandH);
            var rectNum = new XRect(w * 0.74, yTop, w * 0.26 - padX, bandH);
            var pageLabel = $"{i + 1} / {totalPages}";
            gfx.DrawString(hashDraw, font, brushMuted, rectHash, fmtLeft);
            gfx.DrawString(pageLabel, fontPageNum, XBrushes.Black, rectNum, fmtRight);
        }
    }

    /// <summary>Chave canónica e aliases usados em JSON achatado ou aninhado.</summary>
    private static bool TryGetHashDossie(IReadOnlyDictionary<string, string> dados, out string value)
    {
        foreach (var key in new[] { HashDossieKey, "hashDossie", "hash_dossie", "dados.hashDossie", "dados.hash_dossie" })
        {
            if (dados.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                value = v;
                return true;
            }
        }

        value = "";
        return false;
    }

    private static bool TryGetChromeFooterFlag(IReadOnlyDictionary<string, string> dados, out string value)
    {
        foreach (var key in new[]
                 {
                     ChromeFooterKey,
                     "docengineUseChromePageFooter",
                     "docengine_use_chrome_page_footer",
                     "dados.docengineUseChromePageFooter",
                     "dados.docengine_use_chrome_page_footer"
                 })
        {
            if (dados.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                value = v;
                return true;
            }
        }

        value = "";
        return false;
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
