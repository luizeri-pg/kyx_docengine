using System.Globalization;
using KYX.DocEngine.API.Configuration;
using KYX.DocEngine.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;

namespace KYX.DocEngine.API.Data;

public class DocEngineDbContext : DbContext
{
    private readonly SchemaTableOptions _schema;

    public DocEngineDbContext(
        DbContextOptions<DocEngineDbContext> options,
        IOptions<SchemaTableOptions> schemaOptions)
        : base(options)
    {
        _schema = schemaOptions.Value;
    }

    // --- DocEngine PDF ---
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<DocumentJob> DocumentJobs => Set<DocumentJob>();

    // --- Plataforma KYX / Notify (tabelas tb_*) ---
    public DbSet<LogRequisicao> LogRequisicoes => Set<LogRequisicao>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PerfilRole> PerfilRoles => Set<PerfilRole>();
    public DbSet<Integracao> Integracoes => Set<Integracao>();
    public DbSet<LogIntegracao> LogIntegracoes => Set<LogIntegracao>();
    public DbSet<Consumo> Consumos => Set<Consumo>();
    /// <summary>Tabela <c>tb_template</c> (notificações); não confundir com <see cref="Template"/> (PDF).</summary>
    public DbSet<NotificacaoTemplate> NotificacaoTemplates => Set<NotificacaoTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var su = _schema.Usuario;
        var sp = _schema.Perfil;
        var lr = _schema.LogRequisicao;

        modelBuilder.Entity<Template>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<DocumentJob>()
            .HasIndex(d => d.RequisicaoId);

        modelBuilder.Entity<DocumentJob>()
            .HasOne(d => d.Template)
            .WithMany()
            .HasForeignKey(d => d.TemplateId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("tb_usuario");
            entity.HasKey(e => e.Id);

            var idProp = entity.Property(e => e.Id).HasColumnName(su.Id);
            if (su.IdIntegerType)
            {
                idProp.HasConversion(new ValueConverter<string, int>(
                    s => int.Parse(s, CultureInfo.InvariantCulture),
                    i => i.ToString(CultureInfo.InvariantCulture)));
            }

            entity.Property(e => e.Nome).HasColumnName(su.Nome);
            entity.Property(e => e.Email).HasColumnName(su.Email).IsRequired(false);

            if (string.IsNullOrWhiteSpace(su.Login))
            {
                entity.Ignore(e => e.Login);
            }
            else
            {
                entity.Property(e => e.Login).HasColumnName(su.Login);
            }

            entity.Property(e => e.Senha).HasColumnName(su.Senha).IsRequired(false);

            var ativoProp = entity.Property(e => e.Ativo).HasColumnName(su.Ativo);
            if (su.AtivoInverted)
            {
                ativoProp.HasConversion(new ValueConverter<bool, bool>(model => !model, db => !db));
            }

            entity.Property(e => e.CriadoEm).HasColumnName(su.CriadoEm);
            // EF Core não permite duas propriedades na mesma coluna; bases legadas com só dh_inclui, etc.
            if (string.Equals(su.CriadoEm, su.AtualizadoEm, StringComparison.OrdinalIgnoreCase))
                entity.Ignore(e => e.AtualizadoEm);
            else
                entity.Property(e => e.AtualizadoEm).HasColumnName(su.AtualizadoEm);

            if (string.IsNullOrWhiteSpace(su.PerfilId))
            {
                entity.Ignore(e => e.PerfilId);
                entity.Ignore(e => e.Perfil);
                // Evita FK fantasma PerfilId1 (Perfil.Usuarios sem coluna perfil_id na tb_usuario legada).
                modelBuilder.Entity<Perfil>().Ignore(p => p.Usuarios);
            }
            else
            {
                entity.Property(e => e.PerfilId).HasColumnName(su.PerfilId);
                entity.HasOne(e => e.Perfil)
                    .WithMany(p => p.Usuarios)
                    .HasForeignKey(e => e.PerfilId)
                    .OnDelete(DeleteBehavior.Restrict);
            }

            if (su.UniqueEmailIndex)
            {
                entity.HasIndex(e => e.Email).IsUnique();
            }
            else
            {
                entity.HasIndex(e => e.Email);
            }
        });

        modelBuilder.Entity<LogRequisicao>(entity =>
        {
            entity.ToTable("tb_log_requisicao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName(lr.Id);
            entity.Property(e => e.RequisicaoId).HasColumnName(lr.RequisicaoId);
            entity.Property(e => e.UsuarioId).HasColumnName(lr.UsuarioId);
            entity.Property(e => e.CentroCusto).HasColumnName(lr.CentroCusto);
            entity.Property(e => e.RequestPayload).HasColumnName(lr.RequestPayload).HasColumnType("jsonb");
            entity.Property(e => e.ResponsePayload).HasColumnName(lr.ResponsePayload).HasColumnType("jsonb");
            entity.Property(e => e.StatusHttp).HasColumnName(lr.StatusHttp);
            entity.Property(e => e.TempoRespostaMs).HasColumnName(lr.TempoRespostaMs);
            entity.Property(e => e.Erro).HasColumnName(lr.Erro);
            entity.Property(e => e.CriadoEm).HasColumnName(lr.CriadoEm);

            if (string.IsNullOrWhiteSpace(lr.Canal))
            {
                entity.Ignore(e => e.Canal);
            }
            else
            {
                entity.Property(e => e.Canal).HasColumnName(lr.Canal);
                entity.HasIndex(e => e.Canal).HasDatabaseName("ix_tb_log_requisicao_canal");
            }

            entity.HasIndex(e => e.RequisicaoId)
                .IsUnique()
                .HasDatabaseName("ix_tb_log_requisicao_requisicao_id");
            entity.HasIndex(e => e.CriadoEm).HasDatabaseName("ix_tb_log_requisicao_criado_em");
            entity.HasIndex(e => e.CentroCusto).HasDatabaseName("ix_tb_log_requisicao_centro_custo");
        });

        modelBuilder.Entity<Perfil>(entity =>
        {
            entity.HasKey(e => e.Id);

            var perfilIdProp = entity.Property(e => e.Id).HasColumnName(sp.Id);
            if (sp.IdIntegerType)
            {
                perfilIdProp.HasConversion(new ValueConverter<string, int>(
                    s => int.Parse(s, CultureInfo.InvariantCulture),
                    i => i.ToString(CultureInfo.InvariantCulture)));
            }

            entity.Property(e => e.Nome).HasColumnName(sp.Nome).IsRequired(false);

            if (string.IsNullOrWhiteSpace(sp.Descricao))
                entity.Ignore(e => e.Descricao);
            else
                entity.Property(e => e.Descricao).HasColumnName(sp.Descricao).IsRequired(false);

            entity.Property(e => e.CriadoEm).HasColumnName(sp.CriadoEm).IsRequired(false);
            if (string.Equals(sp.CriadoEm, sp.AtualizadoEm, StringComparison.OrdinalIgnoreCase))
                entity.Ignore(e => e.AtualizadoEm);
            else
                entity.Property(e => e.AtualizadoEm).HasColumnName(sp.AtualizadoEm).IsRequired(false);

            entity.HasIndex(e => e.Nome);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Nome);
        });

        modelBuilder.Entity<PerfilRole>(entity =>
        {
            entity.HasKey(e => new { e.PerfilId, e.RoleId });
            entity.HasOne(e => e.Perfil)
                .WithMany(p => p.PerfilRoles)
                .HasForeignKey(e => e.PerfilId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.PerfilRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LogIntegracao>(entity =>
        {
            entity.HasIndex(e => e.RequisicaoId);
            entity.HasIndex(e => e.IntegracaoId);
            entity.HasIndex(e => e.CriadoEm);
            entity.Property(e => e.RequestHeaders).HasColumnType("jsonb");
            entity.Property(e => e.RequestBody).HasColumnType("jsonb");
            entity.Property(e => e.ResponseHeaders).HasColumnType("jsonb");
            entity.Property(e => e.ResponseBody).HasColumnType("jsonb");

            entity.HasOne(e => e.Requisicao)
                .WithMany(r => r.LogIntegracoes)
                .HasForeignKey(e => e.RequisicaoId)
                .HasPrincipalKey(r => r.RequisicaoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Integracao)
                .WithMany(i => i.LogIntegracoes)
                .HasForeignKey(e => e.IntegracaoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Consumo>(entity =>
        {
            entity.HasIndex(e => e.CentroCusto);
            entity.HasIndex(e => e.Canal);
            entity.HasIndex(e => e.CriadoEm);

            entity.HasOne(e => e.Requisicao)
                .WithMany(r => r.Consumos)
                .HasForeignKey(e => e.RequisicaoId)
                .HasPrincipalKey(r => r.RequisicaoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Integracao)
                .WithMany(i => i.Consumos)
                .HasForeignKey(e => e.IntegracaoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NotificacaoTemplate>(entity =>
        {
            entity.Property(e => e.Variaveis).HasColumnType("jsonb");
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var modifiedEntries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in modifiedEntries)
        {
            switch (entry.Entity)
            {
                case Template template:
                    template.UpdatedAt = DateTime.UtcNow;
                    break;
                case DocumentJob job:
                    job.UpdatedAt = DateTime.UtcNow;
                    break;
                case Usuario usuario:
                    usuario.AtualizadoEm = DateTime.UtcNow;
                    break;
                case Perfil perfil:
                    perfil.AtualizadoEm = DateTime.UtcNow;
                    break;
                case Role role:
                    role.AtualizadoEm = DateTime.UtcNow;
                    break;
                case Integracao integracao:
                    integracao.AtualizadoEm = DateTime.UtcNow;
                    break;
                case NotificacaoTemplate nt:
                    nt.AtualizadoEm = DateTime.UtcNow;
                    break;
            }
        }
    }
}
