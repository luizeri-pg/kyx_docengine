using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KYX.DocEngine.API.Data;

/// <summary>
/// Em Development, garante um template HTML mínimo para testar POST /documents/generate sem cadastro manual.
/// Slug: <c>demo_pdf_local</c>
/// </summary>
public static class DemoTemplateSeeder
{
    public const string DemoSlug = "demo_pdf_local";

    public static void TrySeed(DocEngineDbContext db, IHostEnvironment env, ILogger logger)
    {
        if (!env.IsDevelopment())
            return;

        try
        {
            if (db.Templates.AsNoTracking().Any(t => t.Slug == DemoSlug))
                return;

            var html = """
                <!DOCTYPE html>
                <html lang="pt-BR">
                <head>
                  <meta charset="utf-8"/>
                  <style>
                    body { font-family: system-ui, sans-serif; padding: 2rem; color: #111; }
                    h1 { color: #059669; font-size: 1.25rem; }
                    .box { border: 1px solid #ccc; border-radius: 8px; padding: 1rem; margin-top: 1rem; }
                  </style>
                </head>
                <body>
                  <h1>Demonstração KYX DocEngine</h1>
                  <div class="box">
                    <p><strong>Nome:</strong> {{nome}}</p>
                    <p><strong>CPF:</strong> {{cpf}}</p>
                    <p><strong>Data do documento:</strong> {{dataDoc}}</p>
                  </div>
                </body>
                </html>
                """;

            db.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                Slug = DemoSlug,
                Name = "Demo PDF local (HTML)",
                Type = "html",
                Content = html,
                RequiredFields = """["nome","cpf","dataDoc"]""",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            logger.LogInformation("Template de demonstração criado: slug={Slug}", DemoSlug);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível criar o template de demonstração (banco indisponível ou sem permissão).");
        }
    }
}
