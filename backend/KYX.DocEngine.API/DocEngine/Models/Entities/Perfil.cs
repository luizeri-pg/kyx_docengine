using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KYX.DocEngine.API.Models.Entities;

[Table("tb_perfil")]
public class Perfil
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("nome")]
    public string? Nome { get; set; }

    [Column("descricao")]
    public string? Descricao { get; set; }

    [Column("criado_em")]
    public DateTime? CriadoEm { get; set; }

    [Column("atualizado_em")]
    public DateTime? AtualizadoEm { get; set; }

    public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public virtual ICollection<PerfilRole> PerfilRoles { get; set; } = new List<PerfilRole>();
}
