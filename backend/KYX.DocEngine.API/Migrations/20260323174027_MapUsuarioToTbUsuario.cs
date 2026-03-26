using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KYX.DocEngine.API.Migrations;

/// <summary>
/// Alinha o modelo EF com a tabela <c>tb_usuario</c> (mesmo esquema Notify/KYX).
/// Usa SQL idempotente: se a tabela já existir, não falha.
/// </summary>
public partial class MapUsuarioToTbUsuario : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS tb_usuario (
                id text NOT NULL,
                nome text NOT NULL,
                email text NOT NULL,
                senha text NOT NULL,
                perfil_id text NOT NULL,
                ativo boolean NOT NULL DEFAULT TRUE,
                criado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
                atualizado_em timestamp with time zone NOT NULL DEFAULT (timezone('utc', now())),
                CONSTRAINT pk_tb_usuario PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_tb_usuario_email ON tb_usuario (email);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Não removemos tb_usuario: pode ser partilhada com outros serviços.
    }
}
