using System.Buffers.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using KYX.DocEngine.API.Helpers;
using KYX.DocEngine.API.Models.Entities;
using PdfSharp.Pdf;
using SixLabors.ImageSharp;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using PuppeteerSharp;

namespace KYX.DocEngine.API.Services;

public interface IPdfEngineService
{
    /// <param name="pdfIntercaladoNative">Opcional: PDF binário a inserir no sítio de <c>{{DOSSIE_BLOCO_INTERCALADO_HTML}}</c> (template tem de conter esse placeholder).</param>
    Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados, byte[]? pdfIntercaladoNative = null);
}

public interface IHtmlPdfRenderer
{
    /// <param name="pdfIntercaladoNative">Se preenchido, o HTML é partido no placeholder <c>{{DOSSIE_BLOCO_INTERCALADO_HTML}}</c>, geram-se dois PDFs e o PDF nativo é fundido entre eles.</param>
    Task<byte[]> RenderAsync(string htmlTemplate, Dictionary<string, string> dados, byte[]? pdfIntercaladoNative = null);
}

public interface IAcroFormPdfRenderer
{
    Task<byte[]> RenderAsync(string pdfBase64, Dictionary<string, string> dados);
}

public class PdfEngineService : IPdfEngineService
{
    private readonly IHtmlPdfRenderer _htmlRenderer;
    private readonly IAcroFormPdfRenderer _acroFormRenderer;

    public PdfEngineService(IHtmlPdfRenderer htmlRenderer, IAcroFormPdfRenderer acroFormRenderer)
    {
        _htmlRenderer = htmlRenderer;
        _acroFormRenderer = acroFormRenderer;
    }

    public Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados, byte[]? pdfIntercaladoNative = null)
    {
        return template.Type.ToLowerInvariant() switch
        {
            "html" => _htmlRenderer.RenderAsync(template.Content, dados, pdfIntercaladoNative),
            "acroform" => _acroFormRenderer.RenderAsync(template.Content, dados),
            _ => throw new NotSupportedException($"Tipo de template não suportado: {template.Type}")
        };
    }
}

public class HtmlPdfRenderer : IHtmlPdfRenderer
{
    /// <summary>Placeholder exacto no HTML — usado para partir o modelo e inserir um PDF nativo a meio.</summary>
    public const string DossieBlocoIntercaladoPlaceholder = "{{DOSSIE_BLOCO_INTERCALADO_HTML}}";

    /// <summary>
    /// Se <c>true</c> (valores como 1, true, yes, sim), o PDF usa o rodapé nativo do Chromium com
    /// <see cref="PuppeteerSharp.PdfOptions.FooterTemplate"/> — numeração <c>página / total</c> automática.
    /// O hash do dossiê vem de <c>HASH_DOSSIE</c>. Chave reservada: não coloque <c>{{DOCENGINE_USE_CHROME_PAGE_FOOTER}}</c> no HTML.
    /// </summary>
    public const string DocengineChromePageFooterKey = "DOCENGINE_USE_CHROME_PAGE_FOOTER";

    /// <summary>Placeholders de <c>src</c> de imagem que aceitam Base64 cru (sem prefixo <c>data:</c>).</summary>
    private static readonly HashSet<string> ImageSrcKeysAllowRawBase64 = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOGO",
        "LOGO_SIMPLIX_BASE64",
        "IMG_CLIENTE_FOTO",
        "IMG_SELFIE",
        "IMG_DOCUMENTO_FRENTE",
        "IMG_DOCUMENTO_VERSO"
    };

    public async Task<byte[]> RenderAsync(string htmlTemplate, Dictionary<string, string> dados, byte[]? pdfIntercaladoNative = null)
    {
        var injectDados = new Dictionary<string, string>(dados, StringComparer.OrdinalIgnoreCase);
        injectDados.TryGetValue(DocengineChromePageFooterKey, out var chromeFooterFlag);
        injectDados.Remove(DocengineChromePageFooterKey);
        var useChromeFooter = IsTruthyChromeFooterFlag(chromeFooterFlag);

        var chromePath =
            Environment.GetEnvironmentVariable("CHROME_EXECUTABLE_PATH")
            ?? Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");

        var launch = new LaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu"
            }
        };

        if (!string.IsNullOrWhiteSpace(chromePath))
        {
            launch.ExecutablePath = chromePath;
        }
        else
        {
            await new BrowserFetcher().DownloadAsync();
        }

        var hasNativeInterleaved = pdfIntercaladoNative is { Length: > 0 };
        if (hasNativeInterleaved)
        {
            if (!TrySplitHtmlTemplateAtInterleavedSlot(htmlTemplate, out var htmlBefore, out var htmlAfter))
            {
                throw new InvalidOperationException(
                    $"Para pdfIntercaladoBase64, o HTML tem de conter o placeholder exacto {DossieBlocoIntercaladoPlaceholder}.");
            }

            var dadosPartes = new Dictionary<string, string>(injectDados, StringComparer.OrdinalIgnoreCase);
            dadosPartes["DOSSIE_BLOCO_INTERCALADO_HTML"] = string.Empty;

            var injected1 = InjectData(htmlBefore, dadosPartes);
            var injected2 = InjectData(htmlAfter, dadosPartes);

            await using var browser = await Puppeteer.LaunchAsync(launch).ConfigureAwait(false);
            var pdf1 = await RenderHtmlToPdfWithBrowserAsync(browser, injected1, useChromeFooter, dadosPartes).ConfigureAwait(false);
            var pdf2 = await RenderHtmlToPdfWithBrowserAsync(browser, injected2, useChromeFooter, dadosPartes).ConfigureAwait(false);
            return PdfAppendHelper.ConcatInOrder(new[] { pdf1, pdfIntercaladoNative!, pdf2 });
        }

        var html = InjectData(htmlTemplate, injectDados);
        await using var browserSingle = await Puppeteer.LaunchAsync(launch).ConfigureAwait(false);
        return await RenderHtmlToPdfWithBrowserAsync(browserSingle, html, useChromeFooter, injectDados).ConfigureAwait(false);
    }

    /// <summary>
    /// Parte o modelo <strong>antes</strong> de <see cref="InjectData"/> no token <see cref="DossieBlocoIntercaladoPlaceholder"/>,
    /// produzindo dois HTML completos (cabeçalho + corpo parcial + fecho).
    /// </summary>
    private static bool TrySplitHtmlTemplateAtInterleavedSlot(
        string htmlTemplate,
        out string htmlPartFullBeforeSlot,
        out string htmlPartFullAfterSlot)
    {
        htmlPartFullBeforeSlot = null!;
        htmlPartFullAfterSlot = null!;
        var idx = htmlTemplate.IndexOf(DossieBlocoIntercaladoPlaceholder, StringComparison.Ordinal);
        if (idx < 0)
        {
            return false;
        }

        var bodyOpenIdx = htmlTemplate.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyOpenIdx < 0)
        {
            return false;
        }

        var bodyTagEnd = htmlTemplate.IndexOf('>', bodyOpenIdx);
        if (bodyTagEnd < 0)
        {
            return false;
        }

        var bodyContentStart = bodyTagEnd + 1;
        var bodyCloseIdx = htmlTemplate.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyCloseIdx < 0 || bodyCloseIdx < idx)
        {
            return false;
        }

        var headThroughBodyOpen = htmlTemplate[..bodyContentStart];
        var closeThroughEnd = htmlTemplate[bodyCloseIdx..];
        var innerBefore = htmlTemplate[bodyContentStart..idx];
        var innerAfter = htmlTemplate[(idx + DossieBlocoIntercaladoPlaceholder.Length)..bodyCloseIdx];

        htmlPartFullBeforeSlot = headThroughBodyOpen + innerBefore + closeThroughEnd;
        htmlPartFullAfterSlot = headThroughBodyOpen + innerAfter + closeThroughEnd;
        return true;
    }

    private static PdfOptions BuildPdfOptions(bool useChromeFooter, IReadOnlyDictionary<string, string> injectDados)
    {
        var pdfOptions = new PdfOptions
        {
            Format = PuppeteerSharp.Media.PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new PuppeteerSharp.Media.MarginOptions
            {
                Top = "20mm",
                Bottom = "20mm",
                Left = "15mm",
                Right = "15mm"
            }
        };

        if (useChromeFooter)
        {
            injectDados.TryGetValue("HASH_DOSSIE", out var hashRaw);
            var hashHtml = WebUtility.HtmlEncode(hashRaw ?? string.Empty);
            pdfOptions.DisplayHeaderFooter = true;
            pdfOptions.HeaderTemplate = "<div></div>";
            pdfOptions.FooterTemplate =
                "<div style=\"width:100%;font-size:9px;font-family:Arial,Helvetica,sans-serif;padding:4px 14px 0;display:flex;justify-content:space-between;align-items:flex-end;color:#444;box-sizing:border-box;\">"
                + "<span style=\"overflow:hidden;text-overflow:ellipsis;white-space:nowrap;max-width:74%;\">"
                + hashHtml
                + "</span><span style=\"white-space:nowrap;font-weight:700;font-size:11px;\"><span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></span></div>";
            pdfOptions.MarginOptions = new PuppeteerSharp.Media.MarginOptions
            {
                Top = "18mm",
                Bottom = "28mm",
                Left = "15mm",
                Right = "15mm"
            };
        }

        return pdfOptions;
    }

    private static async Task<byte[]> RenderHtmlToPdfWithBrowserAsync(
        IBrowser browser,
        string html,
        bool useChromeFooter,
        Dictionary<string, string> injectDados)
    {
        await using var page = await browser.NewPageAsync().ConfigureAwait(false);
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded },
            Timeout = 120000
        }).ConfigureAwait(false);

        await WaitForDomImagesAsync(page).ConfigureAwait(false);
        var pdfOptions = BuildPdfOptions(useChromeFooter, injectDados);
        return await page.PdfDataAsync(pdfOptions).ConfigureAwait(false);
    }

    /// <summary>
    /// Espera pelas <c>&lt;img&gt;</c> do documento (incl. <c>src</c> em data URI) antes de imprimir para PDF.
    /// </summary>
    private static async Task WaitForDomImagesAsync(IPage page)
    {
        try
        {
            await page.EvaluateFunctionAsync(
                    @"async () => {
                        const imgs = Array.from(document.images || []);
                        await Promise.all(imgs.map(img => {
                            if (img.complete && img.naturalHeight > 0) return Promise.resolve();
                            return new Promise(resolve => {
                                img.addEventListener('load', () => resolve(null), { once: true });
                                img.addEventListener('error', () => resolve(null), { once: true });
                            });
                        }));
                    }")
                .ConfigureAwait(false);
        }
        catch
        {
            // Modelo sem imagens ou evaluate indisponível — não bloquear geração.
        }

        await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
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

    private static string InjectData(string html, Dictionary<string, string> dados)
    {
        foreach (var (key, value) in dados)
        {
            var replacement = CoerceImageSrcForHtml(key, value ?? string.Empty);
            var pattern = $@"\{{\{{\s*{Regex.Escape(key)}\s*\}}\}}";
            html = Regex.Replace(html, pattern, _ => replacement);
        }

        return html;
    }

    /// <summary>
    /// Se o placeholder for de imagem e o valor for só Base64 (sem <c>data:</c> nem URL), monta um data URI para o Chrome renderizar offline.
    /// </summary>
    private static string CoerceImageSrcForHtml(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !ImageSrcKeysAllowRawBase64.Contains(key))
        {
            return value;
        }

        var v = value.Trim();
        string coerced;
        if (v.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            coerced = v;
        }
        else
        {
            var compact = v.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            if (!Base64.IsValid(compact.AsSpan(), out _))
            {
                return value;
            }

            var mime = DetectImageMimeFromBase64Payload(compact) ?? "image/png";
            coerced = $"data:{mime};base64,{compact}";
        }

        return ConvertWebpDataUriToPngForChromePdf(coerced);
    }

    /// <summary>
    /// O Chromium por vezes não rasteriza <c>data:image/webp</c> ao imprimir para PDF — converter para PNG garante o logo no cabeçalho.
    /// </summary>
    private static string ConvertWebpDataUriToPngForChromePdf(string dataUriOrUrl)
    {
        if (!dataUriOrUrl.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return dataUriOrUrl;
        }

        var comma = dataUriOrUrl.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0 || comma >= dataUriOrUrl.Length - 1)
        {
            return dataUriOrUrl;
        }

        byte[] webpBytes;
        try
        {
            webpBytes = Convert.FromBase64String(dataUriOrUrl.AsSpan(comma + 1).Trim().ToString());
        }
        catch
        {
            return dataUriOrUrl;
        }

        if (webpBytes.Length < 12)
        {
            return dataUriOrUrl;
        }

        try
        {
            using var image = Image.Load(webpBytes);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return dataUriOrUrl;
        }
    }

    private static string? DetectImageMimeFromBase64Payload(string compactBase64)
    {
        try
        {
            var bytes = Convert.FromBase64String(compactBase64);
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "image/png";
            }

            if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            {
                return "image/gif";
            }

            if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return "image/webp";
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}

public class AcroFormPdfRenderer : IAcroFormPdfRenderer
{
    public Task<byte[]> RenderAsync(string pdfBase64, Dictionary<string, string> dados)
    {
        var pdfBytes = Convert.FromBase64String(pdfBase64);
        using var inputStream = new MemoryStream(pdfBytes);
        using var outputStream = new MemoryStream();
        var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

        // Some partner templates are plain PDFs (without AcroForm dictionary).
        // In that case we still return a valid PDF instead of failing generation.
        PdfAcroForm? form = null;
        try
        {
            form = document.AcroForm;
        }
        catch
        {
            form = null;
        }

        if (form?.Fields != null)
        {
            foreach (PdfAcroField field in form.Fields)
            {
                if (!dados.TryGetValue(field.Name, out var value))
                {
                    continue;
                }

                if (field is PdfTextField textField)
                {
                    textField.Text = value;
                }
                else if (field is PdfCheckBoxField checkBox)
                {
                    checkBox.Checked = value.ToLowerInvariant() is "true" or "1" or "sim";
                }
            }
        }

        if (form != null)
        {
            form.Elements.Remove("/NeedAppearances");
        }

        document.Save(outputStream, false);
        return Task.FromResult(outputStream.ToArray());
    }
}
