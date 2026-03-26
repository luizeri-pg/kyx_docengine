using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.NotifyHUB.API.Models.Entities;

[Table("tb_perfil_role")]
public class PerfilRole
{
    [Column("perfil_id")]
    public string PerfilId { get; set; } = string.Empty;

    [Column("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [Column("criado_em")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(PerfilId))]
    public virtual Perfil? Perfil { get; set; }

    [ForeignKey(nameof(RoleId))]
    public virtual Role? Role { get; set; }
}

