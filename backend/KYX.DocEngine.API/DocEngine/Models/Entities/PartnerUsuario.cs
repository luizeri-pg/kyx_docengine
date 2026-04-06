using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.DocEngine.API.Models.Entities;

/// <summary>
/// Modelo específico para banco Partner/KYX usando nomes de colunas legados.
/// Usado por PartnerDbAuthService com Dapper ou EF Core com mapeamento explícito.
/// </summary>
[Table("tb_usuario")]
public class PartnerUsuario
{
    [Column("id_usuario")]
    public string Id { get; set; } = string.Empty;

    [Column("str_login")]
    public string Login { get; set; } = string.Empty;

    [Column("str_descricao")]
    public string Nome { get; set; } = string.Empty;

    [Column("str_senha")]
    public string Senha { get; set; } = string.Empty;

    /// <summary>
    /// Coluna 'bloqueado' é invertida: true = bloqueado, false = ativo.
    /// </summary>
    [Column("bloqueado")]
    public bool Bloqueado { get; set; }

    /// <summary>
    /// Helper: retorna true se usuário está ativo (não bloqueado).
    /// </summary>
    public bool Ativo => !Bloqueado;

    [Column("email")]
    public string? Email { get; set; }
}
