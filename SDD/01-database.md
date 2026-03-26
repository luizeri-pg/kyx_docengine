# DocEngine — 01: Database Schema

## Instruções para o Cursor

Crie as migrations do EF Core conforme os modelos abaixo.
Use `Guid` como PK em todas as entidades.
Use `snake_case` nos nomes das tabelas e colunas (configurar via `UseSnakeCaseNamingConvention()`).

---

## Entidades

### 1. `templates`

Armazena os templates cadastrados. Suporta dois tipos: `html` e `acroform`.

```csharp
public class Template
{
    public Guid Id { get; set; }

    /// <summary>
    /// Identificador único usado na requisição. Ex: "fideliza_termo_genero"
    /// </summary>
    public string Slug { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// "html" ou "acroform"
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// Para type=html: conteúdo HTML com variáveis {{variavel}}
    /// Para type=acroform: caminho/referência do arquivo PDF base
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// JSON com os campos obrigatórios. Ex: ["nome","cpf","dataNascimento"]
    /// </summary>
    public string RequiredFields { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

### 2. `document_jobs`

Rastreia cada requisição de geração de documento.

```csharp
public class DocumentJob
{
    public Guid Id { get; set; }

    /// <summary>
    /// ID externo enviado pelo KYX Core (requisicaoId)
    /// </summary>
    public string RequisicaoId { get; set; } = null!;

    public Guid TemplateId { get; set; }
    public Template Template { get; set; } = null!;

    /// <summary>
    /// Centro de custo do solicitante. Ex: "sfairalimentos"
    /// </summary>
    public string CentroCusto { get; set; } = null!;

    /// <summary>
    /// Nome do arquivo final. Ex: "documento.pdf"
    /// </summary>
    public string NomeArquivo { get; set; } = null!;

    /// <summary>
    /// JSON com os dados recebidos para preenchimento
    /// </summary>
    public string InputData { get; set; } = null!;

    /// <summary>
    /// "pending" | "processing" | "completed" | "failed"
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// PDF gerado em base64 (salvo após conclusão)
    /// </summary>
    public string? ResultBase64 { get; set; }

    public string? ErrorMessage { get; set; }

    public long? ProcessingTimeMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

### 3. `request_logs`

Log detalhado de cada requisição recebida (para auditoria).

```csharp
public class RequestLog
{
    public Guid Id { get; set; }
    public string RequisicaoId { get; set; } = null!;
    public string Endpoint { get; set; } = null!;

    /// <summary>
    /// JSON do body recebido
    /// </summary>
    public string RequestBody { get; set; } = null!;

    /// <summary>
    /// JSON do response retornado
    /// </summary>
    public string? ResponseBody { get; set; }

    public int? HttpStatusCode { get; set; }
    public string? UserId { get; set; }
    public string? CentroCusto { get; set; }
    public long? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## DbContext

```csharp
public class DocEngineDbContext : DbContext
{
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<DocumentJob> DocumentJobs => Set<DocumentJob>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Template>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<DocumentJob>()
            .HasIndex(d => d.RequisicaoId);

        modelBuilder.Entity<DocumentJob>()
            .HasOne(d => d.Template)
            .WithMany()
            .HasForeignKey(d => d.TemplateId);
    }
}
```

---

## Migration

Após criar os modelos, gere a migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Índices importantes

```sql
-- Busca rápida de template por slug
CREATE UNIQUE INDEX idx_templates_slug ON templates(slug);

-- Rastreabilidade de jobs por requisicaoId
CREATE INDEX idx_document_jobs_requisicao_id ON document_jobs(requisicao_id);

-- Logs por centro de custo (relatórios de custo)
CREATE INDEX idx_request_logs_centro_custo ON request_logs(centro_custo);
```
