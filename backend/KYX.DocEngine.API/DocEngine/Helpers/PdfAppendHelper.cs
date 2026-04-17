using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace KYX.DocEngine.API.Helpers;

/// <summary>
/// Anexa PDFs ao final de um PDF principal (ex.: dossiê HTML + proposta comercial já em PDF).
/// </summary>
public static class PdfAppendHelper
{
    /// <summary>
    /// Concatena <paramref name="primaryPdf"/> com cada PDF em <paramref name="appendOrdered"/>, na ordem informada.
    /// </summary>
    /// <exception cref="ArgumentException">Base64 inválido ou PDF ilegível.</exception>
    public static byte[] AppendAfter(byte[] primaryPdf, IReadOnlyList<byte[]> appendOrdered)
    {
        if (appendOrdered.Count == 0)
        {
            return primaryPdf;
        }

        using var output = new PdfDocument();
        CopyAllPages(PdfReader.Open(new MemoryStream(primaryPdf), PdfDocumentOpenMode.Import), output);

        foreach (var chunk in appendOrdered)
        {
            if (chunk.Length == 0)
            {
                continue;
            }

            using var annex = PdfReader.Open(new MemoryStream(chunk), PdfDocumentOpenMode.Import);
            CopyAllPages(annex, output);
        }

        using var ms = new MemoryStream();
        output.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>
    /// Concatena vários PDFs pela ordem (ex.: HTML antes do slot + PDF nativo intercalado + HTML depois do slot).
    /// </summary>
    public static byte[] ConcatInOrder(IReadOnlyList<byte[]> pdfsOrdered)
    {
        if (pdfsOrdered.Count == 0)
        {
            return Array.Empty<byte>();
        }

        using var output = new PdfDocument();
        foreach (var chunk in pdfsOrdered)
        {
            if (chunk == null || chunk.Length == 0)
            {
                continue;
            }

            using var part = PdfReader.Open(new MemoryStream(chunk), PdfDocumentOpenMode.Import);
            CopyAllPages(part, output);
        }

        if (output.PageCount == 0)
        {
            return Array.Empty<byte>();
        }

        using var ms = new MemoryStream();
        output.Save(ms, false);
        return ms.ToArray();
    }

    private static void CopyAllPages(PdfDocument from, PdfDocument to)
    {
        for (var i = 0; i < from.PageCount; i++)
        {
            to.AddPage(from.Pages[i]);
        }
    }

    /// <summary>
    /// Decodifica lista de Base64 (cada item = um PDF completo), ignorando entradas vazias/nulas.
    /// </summary>
    public static List<byte[]> DecodePdfBase64List(IEnumerable<string>? base64List)
    {
        var list = new List<byte[]>();
        if (base64List == null)
        {
            return list;
        }

        foreach (var b64 in base64List)
        {
            if (string.IsNullOrWhiteSpace(b64))
            {
                continue;
            }

            try
            {
                list.Add(Convert.FromBase64String(b64.Trim()));
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Item em config.pdfsAnexosBase64 não é Base64 válido.", ex);
            }
        }

        return list;
    }
}
