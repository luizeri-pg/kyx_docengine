using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.NotifyHUB.API.Models.Entities;

[Table("tb_log_integracao")]
public class LogIntegracao
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

    [Column("endpoint")]
    public string? Endpoint { get; set; }

    [Column("metodo")]
    public string? Metodo { get; set; } // GET, POST, etc

    [Column("status_http")]
    public int? StatusHttp { get; set; }

    [Column("request_headers", TypeName = "jsonb")]
    public string? RequestHeaders { get; set; }

    [Column("request_body", TypeName = "jsonb")]
    public string? RequestBody { get; set; }

    [Column("response_headers", TypeName = "jsonb")]
    public string? ResponseHeaders { get; set; }

    [Column("response_body", TypeName = "jsonb")]
    public string? ResponseBody { get; set; }

    [Column("tempo_resposta_ms")]
    public int? TempoRespostaMs { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(RequisicaoId))]
    public virtual LogRequisicao? Requisicao { get; set; }

    [ForeignKey(nameof(IntegracaoId))]
    public virtual Integracao? Integracao { get; set; }
}

