using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.DocEngine.API.Models.Entities;

/// <summary>
/// Mapeia <c>tb_usuario</c>. Nomes das colunas vêm de <c>Schema:Usuario</c> em appsettings (ver <see cref="Configuration.SchemaTableOptions"/>).
/// </summary>
[Table("tb_usuario")]
public class Usuario
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Nome { get; set; } = string.Empty;
    /// <summary>Em bases legadas pode ser NULL na BD.</summary>
    public string? Email { get; set; }
    /// <summary>Login legado (ex.: str_login), quando mapeado em <c>Schema:Usuario:Login</c>.</summary>
    public string? Login { get; set; }
    /// <summary>Hash da senha; em bases legadas a coluna pode ser NULL.</summary>
    public string? Senha { get; set; }
    public string PerfilId { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PerfilId))]
    public virtual Perfil? Perfil { get; set; }
}
