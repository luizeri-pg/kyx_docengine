using System.Text.RegularExpressions;
using KYX.DocEngine.API.Models.Entities;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.IO;
using PuppeteerSharp;

namespace KYX.DocEngine.API.Services;

public interface IPdfEngineService
{
    Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados);
}

public interface IHtmlPdfRenderer
{
    Task<byte[]> RenderAsync(string htmlTemplate, Dictionary<string, string> dados);
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

    public Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados)
    {
        return template.Type.ToLowerInvariant() switch
        {
            "html" => _htmlRenderer.RenderAsync(template.Content, dados),
            "acroform" => _acroFormRenderer.RenderAsync(template.Content, dados),
            _ => throw new NotSupportedException($"Tipo de template não suportado: {template.Type}")
        };
    }
}

public class HtmlPdfRenderer : IHtmlPdfRenderer
{
    public async Task<byte[]> RenderAsync(string htmlTemplate, Dictionary<string, string> dados)
    {
        var html = InjectData(htmlTemplate, dados);
        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });
        return await page.PdfDataAsync(new PdfOptions
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
        });
    }

    private static string InjectData(string html, Dictionary<string, string> dados)
    {
        foreach (var (key, value) in dados)
        {
            html = Regex.Replace(html, $@"\{{\{{\s*{Regex.Escape(key)}\s*\}}\}}", value);
        }

        return html;
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
        var form = document.AcroForm;
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

        if (document.AcroForm != null)
        {
            document.AcroForm.Elements.Remove("/NeedAppearances");
        }

        document.Save(outputStream, false);
        return Task.FromResult(outputStream.ToArray());
    }
}
