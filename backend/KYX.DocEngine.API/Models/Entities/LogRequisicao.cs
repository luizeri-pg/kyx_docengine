using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.NotifyHUB.API.Models.Entities;

[Table("tb_log_requisicao")]
public class LogRequisicao
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("requisicao_id")]
    public string RequisicaoId { get; set; } = string.Empty;

    [Column("usuario_id")]
    public string? UsuarioId { get; set; }

    [Required]
    [Column("canal")]
    public string Canal { get; set; } = string.Empty; // email, sms, whatsapp

    [Column("centro_custo")]
    public string? CentroCusto { get; set; }

    [Column("request_payload", TypeName = "jsonb")]
    public string? RequestPayload { get; set; }

    [Column("response_payload", TypeName = "jsonb")]
    public string? ResponsePayload { get; set; }

    [Column("status_http")]
    public int? StatusHttp { get; set; }

    [Column("tempo_resposta_ms")]
    public int? TempoRespostaMs { get; set; }

    [Column("erro")]
    public string? Erro { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<LogIntegracao> LogIntegracoes { get; set; } = new List<LogIntegracao>();
    public virtual ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
}

