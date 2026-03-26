# DocEngine — 04: Queue (Hangfire + Redis)

## Instruções para o Cursor

Implemente a fila de geração de documentos usando **Hangfire** com **Redis** como storage.
O worker deve buscar o `DocumentJob` pelo ID, gerar o PDF e atualizar o status no banco.

---

## Dependências NuGet

```xml
<PackageReference Include="Hangfire.Core" Version="*" />
<PackageReference Include="Hangfire.AspNetCore" Version="*" />
<PackageReference Include="Hangfire.Redis.StackExchange" Version="*" />
```

---

## Configuração (Program.cs)

```csharp
// Redis connection
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";

// Hangfire com Redis
builder.Services.AddHangfire(config =>
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseRedisStorage(redisConnection, new RedisStorageOptions
        {
            Prefix = "docengine:",
            InvisibilityTimeout = TimeSpan.FromMinutes(10)
        })
);

// Worker server (processa os jobs)
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;  // 5 workers paralelos
    options.Queues = new[] { "documents", "default" };
});

// Expõe o dashboard do Hangfire (restringir a VPN/admin)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthFilter() }
});
```

---

## appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=docengine;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  }
}
```

---

## Interface do Worker

```csharp
public interface IDocumentWorker
{
    Task ProcessAsync(Guid jobId);
}
```

---

## Implementação do Worker

```csharp
public class DocumentWorker : IDocumentWorker
{
    private readonly DocEngineDbContext _db;
    private readonly IPdfEngineService _pdfEngine;
    private readonly ILogger<DocumentWorker> _logger;

    public DocumentWorker(
        DocEngineDbContext db,
        IPdfEngineService pdfEngine,
        ILogger<DocumentWorker> logger)
    {
        _db = db;
        _pdfEngine = pdfEngine;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid jobId)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Busca o job no banco
        var job = await _db.DocumentJobs
            .Include(j => j.Template)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job == null)
        {
            _logger.LogWarning("Job {JobId} não encontrado.", jobId);
            return;
        }

        // 2. Marca como "processing"
        job.Status = "processing";
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            _logger.LogInformation(
                "Iniciando geração do job {JobId} | Template: {Template} | CentroCusto: {CentroCusto}",
                jobId, job.Template.Slug, job.CentroCusto);

            // 3. Desserializa os dados de entrada
            var dados = JsonSerializer.Deserialize<Dictionary<string, string>>(job.InputData)
                ?? new Dictionary<string, string>();

            // 4. Gera o PDF
            var pdfBytes = await _pdfEngine.GenerateAsync(job.Template, dados);

            // 5. Converte para base64
            var base64 = Convert.ToBase64String(pdfBytes);

            // 6. Atualiza o job como "completed"
            job.Status = "completed";
            job.ResultBase64 = base64;
            job.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            job.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Job {JobId} concluído em {Ms}ms.", jobId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar job {JobId}.", jobId);

            // 7. Marca como "failed" e salva o erro
            job.Status = "failed";
            job.ErrorMessage = ex.Message;
            job.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            job.UpdatedAt = DateTime.UtcNow;
        }
        finally
        {
            await _db.SaveChangesAsync();
        }
    }
}
```

---

## Registro do Worker (Program.cs)

```csharp
builder.Services.AddScoped<IDocumentWorker, DocumentWorker>();
```

---

## Enfileiramento no Controller

No `DocumentsController`, após criar o `DocumentJob`:

```csharp
// Enfileira na queue "documents" com prioridade
_backgroundJobClient.Enqueue<IDocumentWorker>(
    "documents",
    w => w.ProcessAsync(job.Id)
);
```

---

## Filtro de autenticação do Dashboard Hangfire

```csharp
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Permite acesso apenas para usuários autenticados
        // Em produção: verificar claim de admin ou IP da VPN
        return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
}
```

---

## Fluxo completo

```
KYX Core
  │
  ▼
POST /documents/generate
  │
  ├─ Valida token
  ├─ Valida template
  ├─ Valida campos obrigatórios
  ├─ Cria DocumentJob (status: "pending")
  └─ Enfileira job no Redis via Hangfire
       │
       ▼
  Hangfire Worker (background)
       │
       ├─ Busca DocumentJob
       ├─ Atualiza status → "processing"
       ├─ Gera PDF (HTML ou AcroForm)
       ├─ Salva base64 no DocumentJob
       └─ Atualiza status → "completed" ou "failed"

KYX Core (polling)
  │
  ▼
GET /documents/status/{jobId}
  └─ Retorna status + base64 quando "completed"
```

---

## Retry Policy

Hangfire tem retry automático por padrão (10 tentativas com backoff exponencial).
Para desabilitar retry em casos de erro de validação:

```csharp
[AutomaticRetry(Attempts = 0)]
public async Task ProcessAsync(Guid jobId) { ... }
```

Para reprocessamento manual via dashboard: acesse `/hangfire`.
