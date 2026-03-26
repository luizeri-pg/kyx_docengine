using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.NotifyHUB.API.Models.Entities;

[Table("tb_template")]
public class Template
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Required]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty; // html, text

    [Required]
    [Column("canal")]
    public string Canal { get; set; } = string.Empty; // email, sms, whatsapp

    [Column("conteudo_html")]
    public string? ConteudoHtml { get; set; }

    [Column("variaveis", TypeName = "jsonb")]
    public string? Variaveis { get; set; } // Array de variáveis disponíveis

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

