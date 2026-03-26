using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KYX.DocEngine.API.Migrations
{
    /// <inheritdoc />
    public partial class DocumentJobInlineTemplateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_document_jobs_templates_template_id",
                table: "document_jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "template_id",
                table: "document_jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "template_snapshot_json",
                table: "document_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_document_jobs_templates_template_id",
                table: "document_jobs",
                column: "template_id",
                principalTable: "templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_document_jobs_templates_template_id",
                table: "document_jobs");

            migrationBuilder.DropColumn(
                name: "template_snapshot_json",
                table: "document_jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "template_id",
                table: "document_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_document_jobs_templates_template_id",
                table: "document_jobs",
                column: "template_id",
                principalTable: "templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
