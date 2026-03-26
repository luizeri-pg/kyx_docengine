using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.DocEngine.API.Models.Entities;

/// <summary>
/// Tabela <c>tb_template</c> do Notify (e-mail/SMS/WhatsApp). **Não** confundir com <see cref="Template"/> (tabela <c>templates</c>, PDF DocEngine).
/// </summary>
[Table("tb_template")]
public class NotificacaoTemplate
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [Column("canal")]
    public string Canal { get; set; } = string.Empty;

    [Column("conteudo_html")]
    public string? ConteudoHtml { get; set; }

    [Column("variaveis", TypeName = "jsonb")]
    public string? Variaveis { get; set; }

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
