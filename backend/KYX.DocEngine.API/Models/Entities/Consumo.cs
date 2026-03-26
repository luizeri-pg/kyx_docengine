using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.NotifyHUB.API.Models.Entities;

[Table("tb_consumo")]
public class Consumo
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("requisicao_id")]
    public string RequisicaoId { get; set; } = string.Empty;

    [Required]
    [Column("integracao_id")]
    public string IntegracaoId { get; set; } = string.Empty;

    [Required]
    [Column("centro_custo")]
    public string CentroCusto { get; set; } = string.Empty;

    [Required]
    [Column("canal")]
    public string Canal { get; set; } = string.Empty; // email, sms, whatsapp

    [Column("valor", TypeName = "decimal(10,2)")]
    public decimal? Valor { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(RequisicaoId))]
    public virtual LogRequisicao? Requisicao { get; set; }

    [ForeignKey(nameof(IntegracaoId))]
    public virtual Integracao? Integracao { get; set; }
}

