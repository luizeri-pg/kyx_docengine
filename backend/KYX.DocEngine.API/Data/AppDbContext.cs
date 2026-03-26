using Microsoft.EntityFrameworkCore;
using KYX.NotifyHUB.API.Models.Entities;

namespace KYX.NotifyHUB.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<PerfilRole> PerfilRoles => Set<PerfilRole>();
    public DbSet<Integracao> Integracoes => Set<Integracao>();
    public DbSet<LogRequisicao> LogRequisicoes => Set<LogRequisicao>();
    public DbSet<LogIntegracao> LogIntegracoes => Set<LogIntegracao>();
    public DbSet<Consumo> Consumos => Set<Consumo>();
    public DbSet<Template> Templates => Set<Template>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Perfil
        modelBuilder.Entity<Perfil>(entity =>
        {
            entity.HasIndex(e => e.Nome).IsUnique();
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Nome).IsUnique();
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PerfilRole (many-to-many)
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

            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Integracao
        modelBuilder.Entity<Integracao>(entity =>
        {
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // LogRequisicao
        modelBuilder.Entity<LogRequisicao>(entity =>
        {
            entity.HasIndex(e => e.RequisicaoId).IsUnique();
            entity.HasIndex(e => e.CriadoEm);
            entity.HasIndex(e => e.Canal);
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // LogIntegracao
        modelBuilder.Entity<LogIntegracao>(entity =>
        {
            entity.HasIndex(e => e.RequisicaoId);
            entity.HasIndex(e => e.IntegracaoId);
            entity.HasIndex(e => e.CriadoEm);
            
            entity.HasOne(e => e.Requisicao)
                .WithMany(r => r.LogIntegracoes)
                .HasForeignKey(e => e.RequisicaoId)
                .HasPrincipalKey(r => r.RequisicaoId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Integracao)
                .WithMany(i => i.LogIntegracoes)
                .HasForeignKey(e => e.IntegracaoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Consumo
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

            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Template
        modelBuilder.Entity<Template>(entity =>
        {
            entity.Property(e => e.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
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
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is Usuario usuario)
                usuario.AtualizadoEm = DateTime.UtcNow;
            else if (entry.Entity is Perfil perfil)
                perfil.AtualizadoEm = DateTime.UtcNow;
            else if (entry.Entity is Role role)
                role.AtualizadoEm = DateTime.UtcNow;
            else if (entry.Entity is Integracao integracao)
                integracao.AtualizadoEm = DateTime.UtcNow;
            else if (entry.Entity is Template template)
                template.AtualizadoEm = DateTime.UtcNow;
        }
    }
}

