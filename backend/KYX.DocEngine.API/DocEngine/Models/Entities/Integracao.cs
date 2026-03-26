using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.DocEngine.API.Models.Entities;

[Table("tb_integracao")]
public class Integracao
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Required]
    [Column("tipo")]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [Column("canal")]
    public string Canal { get; set; } = string.Empty;

    [Required]
    [Column("provedor")]
    public string Provedor { get; set; } = string.Empty;

    [Column("url_base")]
    public string? UrlBase { get; set; }

    [Required]
    [Column("credenciais")]
    public string Credenciais { get; set; } = "{}";

    [Column("ativo")]
    public bool Ativo { get; set; } = true;

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    public virtual ICollection<LogIntegracao> LogIntegracoes { get; set; } = new List<LogIntegracao>();
    public virtual ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
}
