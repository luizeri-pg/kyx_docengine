# DocEngine — 02: API Endpoints

## Instruções para o Cursor

Implemente os controllers abaixo em .NET 10.
Todos os endpoints (exceto `/auth/*`) exigem `[Authorize]` com Bearer Token.
Todos os responses seguem o envelope padrão definido abaixo.

---

## Response Envelope padrão

```csharp
public class ApiResponse<T>
{
    public bool Sucesso { get; set; }
    public string? Mensagem { get; set; }
    public long TempoProcessamento { get; set; }  // em ms
    public string RequisicaoId { get; set; } = null!;
    public T? Resultado { get; set; }
}
```

---

## Endpoints a implementar

### 1. POST `/documents/generate`

**Descrição:** Recebe os dados e o template, enfileira o job de geração e retorna o `jobId` para polling.

**Controller:** `DocumentsController`

#### Request DTO

```csharp
public class GenerateDocumentRequest
{
    [Required]
    public string RequisicaoId { get; set; } = null!;

    [Required]
    public DocumentConfig Config { get; set; } = null!;

    [Required]
    public Dictionary<string, string> Dados { get; set; } = new();
}

public class DocumentConfig
{
    [Required]
    public string Template { get; set; } = null!;  // slug do template

    [Required]
    public string CentroCusto { get; set; } = null!;

    public string NomeArquivo { get; set; } = "documento.pdf";
}
```

#### Response DTO

```csharp
public class GenerateDocumentResponse
{
    public Guid JobId { get; set; }

    /// <summary>
    /// "queued" — job foi aceito e está na fila
    /// </summary>
    public string Status { get; set; } = "queued";
}
```

#### Implementação

```csharp
[HttpPost("generate")]
public async Task<IActionResult> Generate([FromBody] GenerateDocumentRequest request)
{
    var stopwatch = Stopwatch.StartNew();

    // 1. Valida se o template existe
    var template = await _templateService.GetBySlugAsync(request.Config.Template);
    if (template == null)
        return NotFound(new ApiResponse<object>
        {
            Sucesso = false,
            Mensagem = $"Template '{request.Config.Template}' não encontrado.",
            RequisicaoId = request.RequisicaoId,
            TempoProcessamento = stopwatch.ElapsedMilliseconds
        });

    // 2. Valida campos obrigatórios do template
    var missingFields = _templateService.ValidateRequiredFields(template, request.Dados);
    if (missingFields.Any())
        return BadRequest(new ApiResponse<object>
        {
            Sucesso = false,
            Mensagem = $"Campos obrigatórios ausentes: {string.Join(", ", missingFields)}",
            RequisicaoId = request.RequisicaoId,
            TempoProcessamento = stopwatch.ElapsedMilliseconds
        });

    // 3. Cria o DocumentJob com status "pending"
    var job = await _documentJobService.CreateAsync(new DocumentJob
    {
        RequisicaoId = request.RequisicaoId,
        TemplateId = template.Id,
        CentroCusto = request.Config.CentroCusto,
        NomeArquivo = request.Config.NomeArquivo,
        InputData = JsonSerializer.Serialize(request.Dados),
        Status = "pending"
    });

    // 4. Enfileira o job no Hangfire
    _backgroundJobClient.Enqueue<IDocumentWorker>(w => w.ProcessAsync(job.Id));

    stopwatch.Stop();

    return Ok(new ApiResponse<GenerateDocumentResponse>
    {
        Sucesso = true,
        RequisicaoId = request.RequisicaoId,
        TempoProcessamento = stopwatch.ElapsedMilliseconds,
        Resultado = new GenerateDocumentResponse { JobId = job.Id, Status = "queued" }
    });
}
```

---

### 2. GET `/documents/status/{jobId}`

**Descrição:** Polling do status do job. Quando `status = "completed"`, retorna o PDF em base64.

#### Response DTO

```csharp
public class DocumentStatusResponse
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = null!;  // pending | processing | completed | failed
    public string? ErrorMessage { get; set; }
    public DocumentResult? Resultado { get; set; }
}

public class DocumentResult
{
    public string Base64 { get; set; } = null!;
    public string ContentType { get; set; } = "application/pdf";
    public string NomeArquivo { get; set; } = null!;
}
```

#### Implementação

```csharp
[HttpGet("status/{jobId:guid}")]
public async Task<IActionResult> GetStatus(Guid jobId)
{
    var job = await _documentJobService.GetByIdAsync(jobId);
    if (job == null) return NotFound();

    var response = new DocumentStatusResponse
    {
        JobId = job.Id,
        Status = job.Status,
        ErrorMessage = job.ErrorMessage
    };

    if (job.Status == "completed" && job.ResultBase64 != null)
    {
        response.Resultado = new DocumentResult
        {
            Base64 = job.ResultBase64,
            ContentType = "application/pdf",
            NomeArquivo = job.NomeArquivo
        };
    }

    return Ok(new ApiResponse<DocumentStatusResponse>
    {
        Sucesso = true,
        RequisicaoId = job.RequisicaoId,
        TempoProcessamento = job.ProcessingTimeMs ?? 0,
        Resultado = response
    });
}
```

---

### 3. CRUD `/templates`

**Descrição:** Gestão de templates. Usado pelo painel administrativo (Vite frontend).

| Método | Rota | Descrição |
|---|---|---|
| GET | `/templates` | Lista todos os templates ativos |
| GET | `/templates/{id}` | Busca template por ID |
| POST | `/templates` | Cria novo template |
| PUT | `/templates/{id}` | Atualiza template existente |
| DELETE | `/templates/{id}` | Desativa template (soft delete) |

#### Create/Update DTO

```csharp
public class UpsertTemplateRequest
{
    [Required]
    public string Slug { get; set; } = null!;

    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// "html" ou "acroform"
    /// </summary>
    [Required]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Para type=html: conteúdo HTML
    /// Para type=acroform: base64 do PDF
    /// </summary>
    [Required]
    public string Content { get; set; } = null!;

    /// <summary>
    /// Lista de campos obrigatórios para este template
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();
}
```

---

## Middleware de Log

Crie um middleware que intercepta todas as requisições e persiste no `request_logs`:

```csharp
public class RequestLoggingMiddleware
{
    public async Task InvokeAsync(HttpContext context, DocEngineDbContext db)
    {
        var stopwatch = Stopwatch.StartNew();

        // Captura body da requisição
        context.Request.EnableBuffering();
        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Captura body da resposta
        var originalBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        await _next(context);

        responseBuffer.Position = 0;
        var responseBody = await new StreamReader(responseBuffer).ReadToEndAsync();
        responseBuffer.Position = 0;
        await responseBuffer.CopyToAsync(originalBody);

        stopwatch.Stop();

        // Extrai requisicaoId do body se disponível
        string? requisicaoId = null;
        try {
            var json = JsonDocument.Parse(requestBody);
            json.RootElement.TryGetProperty("requisicaoId", out var rid);
            requisicaoId = rid.GetString();
        } catch { }

        await db.RequestLogs.AddAsync(new RequestLog
        {
            RequisicaoId = requisicaoId ?? Guid.NewGuid().ToString(),
            Endpoint = context.Request.Path,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            HttpStatusCode = context.Response.StatusCode,
            DurationMs = stopwatch.ElapsedMilliseconds,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
```

Registre em `Program.cs`:

```csharp
app.UseMiddleware<RequestLoggingMiddleware>();
```
