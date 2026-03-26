# DocEngine — 03: PDF Engine

## Instruções para o Cursor

Implemente o serviço `PdfEngineService` que suporta dois modos de geração:
- **HTML → PDF** via PuppeteerSharp
- **AcroForm → PDF** via PdfSharp

O serviço deve detectar o tipo do template e delegar para o handler correto.

---

## Dependências NuGet

```xml
<PackageReference Include="PuppeteerSharp" Version="*" />
<PackageReference Include="PdfSharp" Version="*" />
```

---

## Interface principal

```csharp
public interface IPdfEngineService
{
    /// <summary>
    /// Gera um PDF a partir de um template e dados.
    /// Retorna o PDF em bytes.
    /// </summary>
    Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados);
}
```

---

## Implementação — Dispatcher

```csharp
public class PdfEngineService : IPdfEngineService
{
    private readonly IHtmlPdfRenderer _htmlRenderer;
    private readonly IAcroFormPdfRenderer _acroFormRenderer;

    public async Task<byte[]> GenerateAsync(Template template, Dictionary<string, string> dados)
    {
        return template.Type switch
        {
            "html"     => await _htmlRenderer.RenderAsync(template.Content, dados),
            "acroform" => await _acroFormRenderer.RenderAsync(template.Content, dados),
            _ => throw new NotSupportedException($"Tipo de template não suportado: {template.Type}")
        };
    }
}
```

---

## Handler 1: HTML → PDF (PuppeteerSharp)

### Como funciona

1. Template contém HTML com variáveis no formato `{{variavel}}`
2. O serviço substitui cada `{{key}}` pelo valor correspondente em `dados`
3. Puppeteer renderiza o HTML em um browser headless e exporta como PDF

### Implementação

```csharp
public class HtmlPdfRenderer : IHtmlPdfRenderer
{
    public async Task<byte[]> RenderAsync(string htmlTemplate, Dictionary<string, string> dados)
    {
        // 1. Substitui variáveis no template
        var html = InjectData(htmlTemplate, dados);

        // 2. Inicializa Puppeteer (baixa Chromium automaticamente se não existir)
        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        await using var page = await browser.NewPageAsync();

        // 3. Carrega o HTML
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.NetworkIdle0 }
        });

        // 4. Gera o PDF
        var pdf = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "20mm", Bottom = "20mm",
                Left = "15mm", Right = "15mm"
            }
        });

        return pdf;
    }

    private string InjectData(string html, Dictionary<string, string> dados)
    {
        foreach (var (key, value) in dados)
        {
            // Substitui {{key}} e {{ key }} (com espaços)
            html = Regex.Replace(html, $@"\{{\{{\s*{Regex.Escape(key)}\s*\}}\}}", value);
        }
        return html;
    }
}
```

### Exemplo de template HTML

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <style>
    body { font-family: Arial, sans-serif; font-size: 12pt; }
    .header { text-align: center; margin-bottom: 30px; }
    .field { margin-bottom: 10px; }
    .label { font-weight: bold; }
  </style>
</head>
<body>
  <div class="header">
    <h1>TERMO DE ADESÃO</h1>
  </div>
  <div class="field">
    <span class="label">Nome:</span> {{nome}}
  </div>
  <div class="field">
    <span class="label">CPF:</span> {{cpf}}
  </div>
  <div class="field">
    <span class="label">Nome da Mãe:</span> {{nomeMae}}
  </div>
  <div class="field">
    <span class="label">Data de Nascimento:</span> {{dataNascimento}}
  </div>
</body>
</html>
```

---

## Handler 2: AcroForm → PDF (PdfSharp)

### Como funciona

1. Template armazena o PDF base em base64 no banco (campo `Content`)
2. O serviço decodifica o base64, abre o PDF com PdfSharp
3. Itera sobre os campos AcroForm do PDF e preenche com os dados
4. Retorna o PDF preenchido em bytes

### Implementação

```csharp
public class AcroFormPdfRenderer : IAcroFormPdfRenderer
{
    public async Task<byte[]> RenderAsync(string pdfBase64, Dictionary<string, string> dados)
    {
        // 1. Decodifica o PDF base
        var pdfBytes = Convert.FromBase64String(pdfBase64);

        using var inputStream = new MemoryStream(pdfBytes);
        using var outputStream = new MemoryStream();

        // 2. Abre o PDF com PdfSharp
        var document = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);

        // 3. Preenche os campos AcroForm
        var form = document.AcroForm;
        if (form?.Fields != null)
        {
            foreach (PdfAcroField field in form.Fields)
            {
                var fieldName = field.Name;
                if (dados.TryGetValue(fieldName, out var value))
                {
                    // Suporta campos de texto e checkbox
                    if (field is PdfTextField textField)
                        textField.Value = new PdfString(value);
                    else if (field is PdfCheckBoxField checkBox)
                        checkBox.Checked = value?.ToLower() is "true" or "1" or "sim";
                }
            }
        }

        // 4. Achata o formulário (impede edição posterior)
        FlattenForm(document);

        document.Save(outputStream);
        return outputStream.ToArray();
    }

    private void FlattenForm(PdfDocument document)
    {
        // Remove campos interativos, deixando apenas o conteúdo visual
        if (document.AcroForm != null)
        {
            document.AcroForm.Elements.Remove("/NeedAppearances");
        }
    }
}
```

---

## Utilitário: Mapear campos de um PDF AcroForm

Use este endpoint auxiliar para descobrir os campos disponíveis em um PDF antes de cadastrar o template:

```csharp
[HttpPost("templates/inspect-pdf")]
public async Task<IActionResult> InspectPdf([FromBody] InspectPdfRequest request)
{
    var pdfBytes = Convert.FromBase64String(request.PdfBase64);
    using var stream = new MemoryStream(pdfBytes);

    var document = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
    var fields = new List<string>();

    if (document.AcroForm?.Fields != null)
    {
        foreach (PdfAcroField field in document.AcroForm.Fields)
        {
            fields.Add(field.Name);
        }
    }

    return Ok(new { fields });
}
```

---

## Registro de serviços (Program.cs)

```csharp
builder.Services.AddScoped<IHtmlPdfRenderer, HtmlPdfRenderer>();
builder.Services.AddScoped<IAcroFormPdfRenderer, AcroFormPdfRenderer>();
builder.Services.AddScoped<IPdfEngineService, PdfEngineService>();
```
