using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KYX.DocEngine.API.Migrations;

/// <summary>
/// Remove <c>request_logs</c> (DocEngine) e padroniza em <c>tb_log_requisicao</c> (mesmo modelo NotifyHUB).
/// Idempotente onde possível (IF EXISTS / IF NOT EXISTS) para DBs que já tinham a tabela legada.
/// </summary>
public partial class UseTbLogRequisicao : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS request_logs;");

        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS tb_log_requisicao (
    id text NOT NULL,
    requisicao_id text NOT NULL,
    usuario_id text NULL,
    canal text NOT NULL,
    centro_custo text NULL,
    request_payload jsonb NULL,
    response_payload jsonb NULL,
    status_http integer NULL,
    tempo_resposta_ms integer NULL,
    erro text NULL,
    criado_em timestamp with time zone NOT NULL,
    CONSTRAINT pk_tb_log_requisicao PRIMARY KEY (id)
);
");

        migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ix_tb_log_requisicao_canal ON tb_log_requisicao (canal);
CREATE INDEX IF NOT EXISTS ix_tb_log_requisicao_centro_custo ON tb_log_requisicao (centro_custo);
CREATE INDEX IF NOT EXISTS ix_tb_log_requisicao_criado_em ON tb_log_requisicao (criado_em);
CREATE INDEX IF NOT EXISTS ix_tb_log_requisicao_requisicao_id ON tb_log_requisicao (requisicao_id);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS tb_log_requisicao;");

        migrationBuilder.CreateTable(
            name: "request_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requisicao_id = table.Column<string>(type: "text", nullable: false),
                endpoint = table.Column<string>(type: "text", nullable: false),
                request_body = table.Column<string>(type: "text", nullable: false),
                response_body = table.Column<string>(type: "text", nullable: true),
                http_status_code = table.Column<int>(type: "integer", nullable: true),
                user_id = table.Column<string>(type: "text", nullable: true),
                centro_custo = table.Column<string>(type: "text", nullable: true),
                duration_ms = table.Column<long>(type: "bigint", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_request_logs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_request_logs_centro_custo",
            table: "request_logs",
            column: "centro_custo");
    }
}
